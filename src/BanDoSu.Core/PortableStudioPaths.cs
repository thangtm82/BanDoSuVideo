namespace BanDoSu.Core;

/// <summary>
/// Self-contained portable folder layout. Paths are anchored to the running EXE,
/// not the current working directory, which may differ for shortcuts or Visual Studio.
/// </summary>
public sealed class PortableStudioPaths
{
    public PortableStudioPaths(string? applicationDirectory = null)
    {
        Root = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory);
    }

    public string Root { get; }
    public string Engine => Path.Combine(Root, "studio");
    public string Projects => Path.Combine(Root, "Projects");
    public string Exports => Path.Combine(Root, "Exports");

    public void Initialize()
    {
        // Creates folder structure but does not pretend an empty studio is a usable engine.
        Directory.CreateDirectory(Engine);
        Directory.CreateDirectory(Projects);
        Directory.CreateDirectory(Exports);
    }

    public bool HasEngine => File.Exists(Path.Combine(Engine, "render.mjs")) &&
        File.Exists(Path.Combine(Engine, "player.html")) &&
        Directory.Exists(Path.Combine(Engine, "tools")) &&
        Directory.Exists(Path.Combine(Engine, "packs"));

    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".venv", "venv", "node_modules", "__pycache__", ".pytest_cache",
        "projects", "build", "out", "dist", ".next"
    };

    public static bool IsValidEngine(string source) =>
        File.Exists(Path.Combine(source, "render.mjs")) &&
        File.Exists(Path.Combine(source, "player.html")) &&
        Directory.Exists(Path.Combine(source, "tools")) &&
        Directory.Exists(Path.Combine(source, "packs"));

    /// <summary>
    /// Copies an existing H2 engine into a staged folder next to the EXE.
    /// Keeps the original untouched. No project/user data or binary dependencies
    /// are copied; Environment Manager installs them afterwards.
    /// Never follows symlinks/junctions and never overwrites an existing engine.
    /// </summary>
    public async Task<string> ImportEngineAsync(string sourceDirectory,
        Action<string>? log = null, CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(sourceDirectory);
        var dest = Path.GetFullPath(Engine);
        if (!IsValidEngine(source))
            throw new IOException("Thư mục nguồn thiếu render.mjs, player.html, tools hoặc packs.");
        if (source.Equals(dest, StringComparison.OrdinalIgnoreCase) ||
            dest.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new IOException("Không thể nhập engine từ chính thư mục studio hoặc thư mục cha của nó.");
        if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any())
            throw new IOException("studio đã có dữ liệu. Để bảo vệ engine hiện tại, hãy sao lưu và làm trống thư mục trước khi nhập.");

        var staging = dest + ".importing-" + Guid.NewGuid().ToString("N");
        try
        {
            await Task.Run(() => CopyEngine(source, staging, log, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsValidEngine(staging))
                throw new IOException("Engine sao chép chưa đủ tệp bắt buộc.");
            // Destination is normally empty because startup creates its directory.
            if (Directory.Exists(dest))
            {
                if (Directory.EnumerateFileSystemEntries(dest).Any())
                    throw new IOException("studio thay đổi trong khi sao chép. Không ghi đè.");
                Directory.Delete(dest);
            }
            Directory.Move(staging, dest);
            log?.Invoke("Đã nhập engine vào " + dest);
            return dest;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void CopyEngine(string source, string dest,
        Action<string>? log, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(dest);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            token.ThrowIfCancellationRequested();
            var name = Path.GetFileName(entry);
            if (Excluded.Contains(name)) continue;
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            var target = Path.Combine(dest, name);
            if (attributes.HasFlag(FileAttributes.Directory))
                CopyEngine(entry, target, log, token);
            else
                File.Copy(entry, target, overwrite: false);
        }
        log?.Invoke("Đã sao chép: " + Path.GetRelativePath(source, dest));
    }
}
