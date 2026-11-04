using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BanDoSu.Core;

/// <summary>
/// Side-by-side H2 engines. Different packs may ship different versions of
/// render.mjs / player.html / engine/, so merging files is intentionally forbidden.
/// Future packs are discovered from packs/*/pack.js without application changes.
/// </summary>
public sealed record PackOption(string Pack, string EngineId, string EngineRoot,
    string Language, string Style)
{
    public string DisplayName => Pack + "  |  " + EngineId;
}

public sealed record ProjectEngineBinding(string Pack, string EngineId);
public sealed record EngineInstallation(string Id, string Root);

public sealed class EngineCatalog
{
    private static readonly Regex SafeId = new("^[a-zA-Z0-9][a-zA-Z0-9_-]{0,80}$",
        RegexOptions.Compiled);
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", ".venv", "venv", "projects", "build", "out",
        "dist", "__pycache__", ".pytest_cache", ".next"
    };

    public StudioSettings Settings { get; }
    public string InstallationsRoot { get; }

    public EngineCatalog(StudioSettings settings)
    {
        Settings = settings;
        InstallationsRoot = Path.Combine(new PortableStudioPaths().Engine, "engines");
    }

    public IReadOnlyList<PackOption> Discover()
    {
        var options = new List<PackOption>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<EngineInstallation> { new("default", Settings.StudioRoot) };
        if (Directory.Exists(InstallationsRoot))
        {
            foreach (var dir in Directory.GetDirectories(InstallationsRoot)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                roots.Add(new(Path.GetFileName(dir), dir));
        }
        foreach (var entry in roots)
        {
            var root = Path.GetFullPath(entry.Root);
            if (!seenRoots.Add(root) || !PortableStudioPaths.IsValidEngine(root)) continue;
            var packsRoot = Path.Combine(root, "packs");
            if (!Directory.Exists(packsRoot)) continue;
            foreach (var dir in Directory.GetDirectories(packsRoot))
            {
                var pack = Path.GetFileName(dir);
                if (!SafeId.IsMatch(pack) || !File.Exists(Path.Combine(dir, "pack.js"))) continue;
                options.Add(new PackOption(pack, entry.Id, root,
                    pack.Equals("bando", StringComparison.OrdinalIgnoreCase) ? "vi" : "en",
                    "flat"));
            }
        }
        return options.OrderBy(x => x.Pack, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.EngineId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string GetProjectPack(string projectDirectory)
    {
        var cfg = Path.Combine(projectDirectory, "config.json");
        if (!File.Exists(cfg)) return "";
        using var document = JsonDocument.Parse(File.ReadAllText(cfg));
        return document.RootElement.TryGetProperty("pack", out var pack) &&
            pack.ValueKind == JsonValueKind.String ? pack.GetString() ?? "" : "";
    }

    public static string BindingPath(string projectDirectory) =>
        Path.Combine(projectDirectory, ".bandosu-engine.json");

    public static ProjectEngineBinding? ReadBinding(string projectDirectory)
    {
        var path = BindingPath(projectDirectory);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ProjectEngineBinding>(File.ReadAllText(path));
        }
        catch (JsonException) { return null; }
    }

    public static void WriteBinding(string projectDirectory, PackOption profile)
    {
        // This file is only application metadata; never rewrite H2 config.json.
        var path = BindingPath(projectDirectory);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(
            new ProjectEngineBinding(profile.Pack, profile.EngineId), JsonOptions.Pretty));
        File.Move(tmp, path, true);
    }

    public PackOption ResolveForProject(string projectDirectory)
    {
        var actualPack = GetProjectPack(projectDirectory);
        var binding = ReadBinding(projectDirectory);
        var matches = Discover()
            .Where(p => p.Pack.Equals(actualPack, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
            throw new FileNotFoundException("Không có H2 Engine hỗ trợ pack '" + actualPack +
                "'. Hãy nhập engine chứa packs/" + actualPack + "/pack.js vào Quản lý Engine.");
        if (binding != null)
        {
            var pinned = matches.FirstOrDefault(p =>
                p.EngineId.Equals(binding.EngineId, StringComparison.OrdinalIgnoreCase));
            if (pinned != null) return pinned;
            throw new FileNotFoundException("Dự án đang dùng engine '" + binding.EngineId +
                "' nhưng engine này chưa được cài. Hãy cài lại đúng engine hoặc chuyển engine có xác nhận.");
        }
        // Existing projects without binding retain the original default engine when it supports their pack.
        return matches.FirstOrDefault(x => x.EngineId == "default") ?? matches[0];
    }

    /// <summary>
    /// Import a user-supplied ZIP (such as h2-terrain-studio.zip). The archive
    /// may contain a single engine at its root, kit/, or skills/*/kit/.
    /// Extracts into temporary storage, rejects traversal/links, and never
    /// replaces an installed engine.
    /// </summary>
    public async Task<PackOption[]> ImportArchiveAsync(string archivePath, string installationId,
        CancellationToken ct = default)
    {
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Không tìm thấy ZIP engine.", archivePath);
        Directory.CreateDirectory(InstallationsRoot);
        var extraction = Path.Combine(InstallationsRoot, ".zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extraction);
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            if (zip.Entries.Count > 20000)
                throw new InvalidDataException("ZIP chứa quá nhiều tệp.");
            const long maxBytes = 4L * 1024 * 1024 * 1024;
            long cumulative = 0;
            var normalizedRoot = Path.GetFullPath(extraction).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var raw = entry.FullName.Replace('\\', '/');
                if (raw.StartsWith('/') || raw.Split('/').Any(part => part == "..") ||
                    raw.IndexOf(':') >= 0)
                    throw new InvalidDataException("ZIP chứa đường dẫn không an toàn.");
                var kind = (entry.ExternalAttributes >> 16) & 0xF000;
                if (kind == 0xA000)
                    throw new InvalidDataException("ZIP có symbolic link. Không nhập.");
                cumulative += entry.Length;
                if (cumulative > maxBytes)
                    throw new InvalidDataException("ZIP quá lớn sau khi giải nén.");
                var full = Path.GetFullPath(Path.Combine(extraction,
                    raw.Replace('/', Path.DirectorySeparatorChar)));
                if (!full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("ZIP vượt ngoài thư mục tạm.");
                if (raw.EndsWith('/'))
                {
                    Directory.CreateDirectory(full);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await using var output = new FileStream(full, FileMode.CreateNew, FileAccess.Write);
                await using var input = entry.Open();
                await input.CopyToAsync(output, ct);
            }
            var candidates = new List<string>();
            if (PortableStudioPaths.IsValidEngine(extraction)) candidates.Add(extraction);
            foreach (var dir in Directory.EnumerateDirectories(extraction, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (Path.GetFileName(dir).Equals("kit", StringComparison.OrdinalIgnoreCase) &&
                    PortableStudioPaths.IsValidEngine(dir))
                    candidates.Add(dir);
            }
            if (candidates.Count != 1)
                throw new InvalidDataException("ZIP phải chứa đúng một engine H2 hợp lệ (thư mục kit). " +
                    "Đã tìm thấy: " + candidates.Count);
            var installed = await ImportAsync(candidates[0], installationId, ct);
            // A skill ZIP may ship authoring workflows alongside kit/ (e.g.
            // skills/kich-ban-terrain). Install them with the selected engine,
            // never into the bando skill location or another engine version.
            var skillsSource = Path.Combine(extraction, "skills");
            if (Directory.Exists(skillsSource))
            {
                var skillTargetRoot = Path.Combine(InstallationsRoot, installationId, "skills");
                foreach (var sourceSkill in Directory.GetDirectories(skillsSource, "kich-ban-*"))
                {
                    var skillName = Path.GetFileName(sourceSkill);
                    if (!SafeId.IsMatch(skillName) ||
                        !File.Exists(Path.Combine(sourceSkill, "SKILL.md"))) continue;
                    var destination = Path.Combine(skillTargetRoot, skillName);
                    if (!Directory.Exists(destination))
                        CopyFiles(sourceSkill, destination, ct);
                }
            }
            return installed;
        }
        finally
        {
            if (Directory.Exists(extraction)) Directory.Delete(extraction, recursive: true);
        }
    }

    public async Task<PackOption[]> ImportAsync(string sourceRoot, string installationId,
        CancellationToken ct = default)
    {
        if (!SafeId.IsMatch(installationId) || installationId.Equals("default", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Tên engine chỉ được gồm chữ Latin, số, _ hoặc -.");
        var source = Path.GetFullPath(sourceRoot);
        if (!PortableStudioPaths.IsValidEngine(source))
            throw new IOException("Engine nguồn phải có render.mjs, player.html, tools/ và packs/.");
        var dest = Path.Combine(InstallationsRoot, installationId);
        if (Directory.Exists(dest)) throw new IOException(
            "Engine '" + installationId + "' đã tồn tại. Hãy dùng tên phiên bản mới để cập nhật mà không ghi đè bản cũ.");
        if (dest.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Không thể nhập engine từ thư mục cha chứa nơi cài đích.");
        Directory.CreateDirectory(InstallationsRoot);
        var stage = dest + ".staging-" + Guid.NewGuid().ToString("N");
        try
        {
            await Task.Run(() => CopyFiles(source, stage, ct), ct);
            ct.ThrowIfCancellationRequested();
            if (!PortableStudioPaths.IsValidEngine(stage))
                throw new IOException("Dữ liệu engine được sao chép không hợp lệ.");
            Directory.Move(stage, dest);
            var options = Discover().Where(x => x.EngineId == installationId).ToArray();
            if (options.Length == 0) throw new IOException(
                "Engine đã nhập nhưng không có pack hợp lệ (packs/*/pack.js).");
            return options;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
    }

    private static void CopyFiles(string source, string target, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(target);
        foreach (var item in Directory.EnumerateFileSystemEntries(source))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(item);
            if (Ignored.Contains(name)) continue;
            var attrs = File.GetAttributes(item);
            if (attrs.HasFlag(FileAttributes.ReparsePoint)) continue;
            var dst = Path.Combine(target, name);
            if (attrs.HasFlag(FileAttributes.Directory)) CopyFiles(item, dst, ct);
            else File.Copy(item, dst, overwrite: false);
        }
    }
}
