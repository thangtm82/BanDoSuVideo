using Microsoft.Win32;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BanDoSu.Core;

public enum DependencyKind
{
    Engine, Node, Npm, Python, Ffmpeg, Ffprobe, NpmPackages,
    PlaywrightChromium, PythonPackages, WebView2
}

public sealed record DependencyResult(
    DependencyKind Kind, string Name, bool Installed, string Details,
    bool CanInstall = true);

public sealed record EnvironmentProgress(string Action, string Message, int Completed, int Total);

/// <summary>
/// Explicit, user-triggered environment setup. Never installs silently at startup.
/// Global Windows packages are acquired through WinGet; project dependencies are
/// installed in the engine folder and Python packages in a private .venv.
/// </summary>
public sealed class EnvironmentManager
{
    private readonly StudioSettings _settings;
    private readonly StudioProcess _runner = new();
    private readonly string _engineRoot;

    public EnvironmentManager(StudioSettings settings, string? engineRoot = null)
    {
        _settings = settings;
        _engineRoot = Path.GetFullPath(engineRoot ?? settings.StudioRoot);
    }

    public static IReadOnlyList<DependencyKind> InstallationOrder { get; } =
    [
        DependencyKind.Engine, DependencyKind.Node, DependencyKind.Npm,
        DependencyKind.Python, DependencyKind.Ffmpeg, DependencyKind.Ffprobe,
        DependencyKind.WebView2, DependencyKind.NpmPackages,
        DependencyKind.PlaywrightChromium, DependencyKind.PythonPackages
    ];

    public static string DisplayName(DependencyKind kind) => kind switch
    {
        DependencyKind.Engine => "Engine H2 Studio",
        DependencyKind.Node => "Node.js",
        DependencyKind.Npm => "npm",
        DependencyKind.Python => "Python",
        DependencyKind.Ffmpeg => "FFmpeg",
        DependencyKind.Ffprobe => "FFprobe",
        DependencyKind.NpmPackages => "Các gói npm của engine",
        DependencyKind.PlaywrightChromium => "Playwright và Chromium",
        DependencyKind.PythonPackages => "Các thư viện Python trong .venv",
        DependencyKind.WebView2 => "Microsoft WebView2 Runtime",
        _ => kind.ToString()
    };

    private string Engine => _engineRoot;
    private string PrivatePython => Path.Combine(Engine, ".venv", "Scripts", "python.exe");
    private NodePackageTools NodeTools => new(_settings.NodeExecutable, Engine);
    private string FfprobeExecutable => SiblingTool(_settings.FfmpegExecutable, "ffprobe.exe");

    private static string SiblingTool(string configured, string sibling)
    {
        if (!Path.IsPathRooted(configured)) return sibling;
        var candidate = Path.Combine(Path.GetDirectoryName(configured)!, sibling);
        return File.Exists(candidate) ? candidate : sibling;
    }

    private async Task<ProcessResult?> ProbeAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(18));
            return await _runner.RunAsync(executable, arguments, workingDirectory,
                cancellationToken: timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or
            IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return null;
        }
    }

    public async Task<DependencyResult> CheckAsync(DependencyKind kind, CancellationToken cancellationToken = default)
    {
        var name = DisplayName(kind);
        if (kind == DependencyKind.Engine)
        {
            var valid = PortableStudioPaths.IsValidEngine(Engine);
            return new(kind, name, valid,
                valid ? "Engine có render.mjs, player.html, tools và packs."
                    : "studio chưa có engine H2 đầy đủ. Vào Cài đặt → Nhập engine H2 vào studio.",
                false);
        }

        if (kind == DependencyKind.WebView2)
        {
            var version = DetectWebView2();
            return new(kind, name, !string.IsNullOrWhiteSpace(version),
                version is null ? "Chưa phát hiện WebView2 Runtime." : "Phiên bản " + version);
        }

        if (kind is DependencyKind.NpmPackages or DependencyKind.PlaywrightChromium or DependencyKind.PythonPackages)
        {
            if (!File.Exists(Path.Combine(Engine, "render.mjs")))
                return new(kind, name, false, "Chưa cấu hình engine H2 Studio.", false);
        }

        var cwd = Directory.Exists(Engine) ? Engine : Environment.CurrentDirectory;
        ProcessResult? result;
        switch (kind)
        {
            case DependencyKind.Node:
                result = await ProbeAsync(_settings.NodeExecutable, ["--version"], cwd, cancellationToken);
                return FromProcess(kind, name, result);
            case DependencyKind.Npm:
                try
                {
                    var cli = NodeTools.ResolveNpmCli();
                    result = await ProbeAsync(NodeTools.FindNodeExecutable()!, [cli, "--version"], cwd, cancellationToken);
                    return FromProcess(kind, name, result);
                }
                catch (FileNotFoundException ex)
                {
                    return new(kind, name, false, ex.Message);
                }
            case DependencyKind.Python:
                result = await ProbeAsync(_settings.PythonExecutable, ["--version"], cwd, cancellationToken);
                return FromProcess(kind, name, result);
            case DependencyKind.Ffmpeg:
                result = await ProbeAsync(_settings.FfmpegExecutable, ["-version"], cwd, cancellationToken);
                return FromProcess(kind, name, result);
            case DependencyKind.Ffprobe:
                result = await ProbeAsync(FfprobeExecutable, ["-version"], cwd, cancellationToken);
                return FromProcess(kind, name, result);
            case DependencyKind.NpmPackages:
                if (!File.Exists(Path.Combine(Engine, "package.json")))
                    return new(kind, name, false, "Không có package.json trong Engine.", false);
                try
                {
                    var cli = NodeTools.ResolveNpmCli();
                    result = await ProbeAsync(NodeTools.FindNodeExecutable()!,
                        [cli, "ls", "--depth=0", "--omit=dev"], Engine, cancellationToken);
                    return FromProcess(kind, name, result);
                }
                catch (FileNotFoundException ex)
                {
                    return new(kind, name, false, ex.Message);
                }
            case DependencyKind.PlaywrightChromium:
                // The same Playwright package used by the engine resolves its browser path.
                // No downloads or package updates occur during this detection.
                const string check = "const fs=require('fs');try{const p=require('playwright');const f=p.chromium.executablePath();if(!fs.existsSync(f))process.exit(2);console.log(f)}catch(e){console.error(e.message);process.exit(2)}";
                result = await ProbeAsync(_settings.NodeExecutable, ["-e", check], Engine, cancellationToken);
                return FromProcess(kind, name, result);
            case DependencyKind.PythonPackages:
                if (!File.Exists(PrivatePython))
                    return new(kind, name, false, "Chưa có Python .venv của engine.");
                const string probe = "import importlib.util,sys;missing=[p for p in ('numpy','scipy','soundfile') if importlib.util.find_spec(p) is None];print('OK' if not missing else 'Thiếu: '+', '.join(missing));sys.exit(bool(missing))";
                result = await ProbeAsync(PrivatePython, ["-c", probe], Engine, cancellationToken);
                return FromProcess(kind, name, result);
            default: return new(kind, name, false, "Chưa hỗ trợ.");
        }
    }

    private static DependencyResult FromProcess(DependencyKind kind, string name, ProcessResult? result)
    {
        var valid = result is { ExitCode: 0 };
        var output = result?.Output.Trim() ?? "";
        if (output.Length > 250) output = output[..250] + "...";
        return new(kind, name, valid, valid ? (string.IsNullOrWhiteSpace(output) ? "Đã cài đặt" : output)
            : (string.IsNullOrWhiteSpace(output) ? "Chưa tìm thấy hoặc kiểm tra thất bại." : output));
    }

    private static string? DetectWebView2()
    {
        if (!OperatingSystem.IsWindows()) return null;
        const string key = @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F1E7E3E9-5D2E-4EBD-AB4B-76F3B3CFB65D}";
        // WebView2 Runtime product key: {F1E7E3E9-5D2E-4EBD-AB4B-76F3B3CFB65D}
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var view in new[] { key, @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F1E7E3E9-5D2E-4EBD-AB4B-76F3B3CFB65D}" })
        {
            try
            {
                var value = hive.OpenSubKey(view)?.GetValue("pv") as string;
                if (!string.IsNullOrWhiteSpace(value) && value != "0.0.0.0") return value;
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    public async Task<IReadOnlyList<DependencyResult>> CheckAllAsync(
        IProgress<EnvironmentProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var results = new List<DependencyResult>();
        var total = InstallationOrder.Count;
        foreach (var kind in InstallationOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new("Kiểm tra", DisplayName(kind), results.Count, total));
            results.Add(await CheckAsync(kind, cancellationToken).ConfigureAwait(false));
            progress?.Report(new("Kiểm tra", DisplayName(kind), results.Count, total));
        }
        return results;
    }

    public static string? WinGetId(DependencyKind kind) => kind switch
    {
        DependencyKind.Node or DependencyKind.Npm => "OpenJS.NodeJS.LTS",
        DependencyKind.Python => "Python.Python.3.12",
        DependencyKind.Ffmpeg or DependencyKind.Ffprobe => "Gyan.FFmpeg",
        DependencyKind.WebView2 => "Microsoft.EdgeWebView2Runtime",
        _ => null
    };

    public async Task<ProcessResult> InstallAsync(DependencyKind kind,
        Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (kind == DependencyKind.Engine)
            throw new InvalidOperationException("Không tự cài engine: hãy chọn thư mục H2 Studio đã tải.");
        var winGetId = WinGetId(kind);
        if (winGetId is not null)
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Chỉ hỗ trợ Windows.");
            log?.Invoke("Cài qua WinGet: " + winGetId);
            var args = new[] { "install", "--id", winGetId, "--exact", "--source", "winget",
                "--accept-package-agreements", "--accept-source-agreements", "--silent",
                "--disable-interactivity" };
            var result = await _runner.RunAsync("winget", args, Environment.CurrentDirectory, log, cancellationToken);
            if (result.ExitCode == 0)
                log?.Invoke("Windows có thể yêu cầu mở lại ứng dụng để cập nhật PATH.");
            return result;
        }
        if (!File.Exists(Path.Combine(Engine, "render.mjs")))
            throw new InvalidOperationException("Chưa chọn Engine H2 Studio hợp lệ.");

        switch (kind)
        {
            case DependencyKind.NpmPackages:
            {
                if (!File.Exists(Path.Combine(Engine, "package.json")))
                    throw new FileNotFoundException("Không có package.json của H2 Studio.");
                var verb = File.Exists(Path.Combine(Engine, "package-lock.json")) ? "ci" : "install";
                log?.Invoke("Chạy npm " + verb + " tại " + Engine);
                var cli = NodeTools.ResolveNpmCli();
                return await _runner.RunAsync(NodeTools.FindNodeExecutable()!,
                    [cli, verb], Engine, log, cancellationToken);
            }
            case DependencyKind.PlaywrightChromium:
                // Use the Playwright version installed in the engine; never call
                // npx.cmd or install an arbitrary latest Playwright package.
                var playwrightCli = NodeTools.ResolvePlaywrightCli();
                log?.Invoke("Cài Chromium theo Playwright của engine: " + playwrightCli);
                return await _runner.RunAsync(NodeTools.FindNodeExecutable() ??
                    throw new FileNotFoundException("Không tìm thấy node.exe trong PATH."),
                    [playwrightCli, "install", "chromium"], Engine, log, cancellationToken);
            case DependencyKind.PythonPackages:
            {
                if (!File.Exists(PrivatePython))
                {
                    var create = await _runner.RunAsync(_settings.PythonExecutable,
                        ["-m", "venv", Path.Combine(Engine, ".venv")], Engine, log, cancellationToken);
                    if (create.ExitCode != 0) return create;
                }
                var req = Path.Combine(Engine, "requirements.txt");
                var args = File.Exists(req)
                    ? new[] { "-m", "pip", "install", "-r", req }
                    : new[] { "-m", "pip", "install", "numpy", "scipy", "soundfile" };
                log?.Invoke("Cài thư viện Python vào .venv của engine.");
                return await _runner.RunAsync(PrivatePython, args, Engine, log, cancellationToken);
            }
            default: throw new NotSupportedException("Không có trình cài cho " + DisplayName(kind));
        }
    }
}
