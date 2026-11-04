using System.Text.Json;
using System.Text.Json.Nodes;

namespace BanDoSu.Core;

public sealed record ProjectSummary(string Name, string Directory, string Title, bool IsLegacy = false);

/// <summary>
/// Resolves projects in the independent workspace, while keeping projects created
/// by older Studio versions under engine/projects accessible.
/// </summary>
public sealed class ProjectFiles
{
    public StudioSettings Settings { get; }
    public ProjectFiles(StudioSettings settings) => Settings = settings;

    public string ProjectsRoot => Path.GetFullPath(Settings.WorkspaceRoot);
    public string LegacyProjectsRoot => Path.GetFullPath(Path.Combine(Settings.StudioRoot, "projects"));
    public string? ExternalLegacyProjectsRoot => string.IsNullOrWhiteSpace(Settings.LegacyEngineRoot) ? null :
        Path.GetFullPath(Path.Combine(Settings.LegacyEngineRoot, "projects"));
    public string? ExternalLegacyWorkspaceRoot => string.IsNullOrWhiteSpace(Settings.LegacyWorkspaceRoot) ? null :
        Path.GetFullPath(Settings.LegacyWorkspaceRoot);

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9_-]{0,80}$"))
            throw new ArgumentException("Tên dự án không hợp lệ.", nameof(name));
    }

    public static void ValidateRoots(StudioSettings settings)
    {
        var engine = Path.GetFullPath(settings.StudioRoot).TrimEnd(Path.DirectorySeparatorChar);
        var workspace = Path.GetFullPath(settings.WorkspaceRoot).TrimEnd(Path.DirectorySeparatorChar);
        var exports = Path.GetFullPath(settings.ExportRoot).TrimEnd(Path.DirectorySeparatorChar);
        static bool Inside(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (Inside(workspace, engine) || Inside(exports, engine))
            throw new ArgumentException("Workspace và thư mục xuất phải nằm ngoài thư mục engine H2.");
        if (Inside(workspace, exports) || Inside(exports, workspace))
            throw new ArgumentException("Workspace và thư mục xuất không được lồng nhau.");
        if (Path.GetPathRoot(workspace)?.Equals(workspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true)
            throw new ArgumentException("Không thể dùng thư mục gốc ổ đĩa làm workspace.");
    }

    private static string InRoot(string root, string name)
    {
        ValidateName(name);
        return Path.GetFullPath(Path.Combine(root, name));
    }

    public string ResolveProject(string name)
    {
        var current = InRoot(ProjectsRoot, name);
        if (Directory.Exists(current) && File.Exists(Path.Combine(current, "config.json"))) return current;
        var legacy = InRoot(LegacyProjectsRoot, name);
        if (Directory.Exists(legacy) && File.Exists(Path.Combine(legacy, "config.json"))) return legacy;
        if (ExternalLegacyWorkspaceRoot is string oldWorkspace)
        {
            var older = InRoot(oldWorkspace, name);
            if (Directory.Exists(older) && File.Exists(Path.Combine(older, "config.json"))) return older;
        }
        if (ExternalLegacyProjectsRoot is string external)
        {
            var older = InRoot(external, name);
            if (Directory.Exists(older) && File.Exists(Path.Combine(older, "config.json"))) return older;
        }
        return current;
    }

    public bool Exists(string name) =>
        Directory.Exists(InRoot(ProjectsRoot, name)) ||
        Directory.Exists(InRoot(LegacyProjectsRoot, name)) ||
        (ExternalLegacyProjectsRoot is string external && Directory.Exists(InRoot(external, name))) ||
        (ExternalLegacyWorkspaceRoot is string oldWorkspace && Directory.Exists(InRoot(oldWorkspace, name)));

    public IReadOnlyList<ProjectSummary> List()
    {
        var items = new Dictionary<string, ProjectSummary>(StringComparer.OrdinalIgnoreCase);
        var folders = new List<(string folder, bool legacy)>();
        if (ExternalLegacyProjectsRoot is string external) folders.Add((external, true));
        if (ExternalLegacyWorkspaceRoot is string oldWorkspace) folders.Add((oldWorkspace, true));
        folders.Add((LegacyProjectsRoot, true));
        folders.Add((ProjectsRoot, false));
        foreach (var (folder, legacy) in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var directory in Directory.GetDirectories(folder))
            {
                var config = Path.Combine(directory, "config.json");
                if (!File.Exists(config)) continue;
                var name = Path.GetFileName(directory);
                var title = name;
                try { title = ReadJson(config)["title"]?.GetValue<string>() ?? name; }
                catch (Exception ex) when (ex is JsonException or IOException) { }
                items[name] = new ProjectSummary(name, directory, title, legacy);
            }
        }
        return items.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public string FileInProject(string project, params string[] parts)
    {
        var root = ResolveProject(project);
        var full = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Đường dẫn nằm ngoài thư mục dự án.");
        return full;
    }

    /// <summary>
    /// The upstream new_project.py creates under the H2 engine's projects folder.
    /// Move a newly generated project to the independent workspace after successful creation.
    /// Never overwrite an existing project.
    /// </summary>
    public string MoveNewProjectToWorkspace(string name, string? sourceEngineRoot = null)
    {
        var sourceProjects = sourceEngineRoot is null ? LegacyProjectsRoot
            : Path.GetFullPath(Path.Combine(sourceEngineRoot, "projects"));
        var source = InRoot(sourceProjects, name);
        var destination = InRoot(ProjectsRoot, name);
        if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "config.json")))
            throw new FileNotFoundException("Engine chưa tạo dự án hợp lệ.", Path.Combine(source, "config.json"));
        if (Directory.Exists(destination))
            throw new IOException("Dự án đã tồn tại trong workspace.");
        ValidateRoots(Settings);
        Directory.CreateDirectory(ProjectsRoot);
        // Directory.Move is atomic on a single volume; different volumes require
        // a copy-and-verify migration. Never silently delete the engine project.
        if (!string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
        {
            var staging = destination + ".migrating-" + Guid.NewGuid().ToString("N");
            try
            {
                CopyDirectory(source, staging);
                var sourceFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                var targetFiles = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
                if (sourceFiles.Length != targetFiles.Length)
                    throw new IOException("Số lượng tệp dự án sao chép không khớp.");
                foreach (var original in sourceFiles)
                {
                    var relative = Path.GetRelativePath(source, original);
                    var copied = Path.Combine(staging, relative);
                    if (!File.Exists(copied) || new FileInfo(original).Length != new FileInfo(copied).Length)
                        throw new IOException("Tệp sao chép không hợp lệ: " + relative);
                }
                Directory.Move(staging, destination);
            }
            catch
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
                throw;
            }
            // Data has been verified in Workspace. Never delete an existing
            // unrelated engine project; this source is created by the current command.
            Directory.Delete(source, recursive: true);
        }
        else Directory.Move(source, destination);
        return destination;
    }

    /// <summary>
    /// Optional, non-destructive migration of a legacy engine project.
    /// The original project is kept as a backup, with Workspace taking precedence.
    /// </summary>
    public string CopyLegacyProjectToWorkspace(string name)
    {
        ValidateName(name);
        ValidateRoots(Settings);
        var source = ResolveProject(name);
        var destination = InRoot(ProjectsRoot, name);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Dự án đã nằm trong Workspace.");
        if (!File.Exists(Path.Combine(source, "config.json")))
            throw new FileNotFoundException("Không tìm thấy dự án cũ trong engine.");
        if (Directory.Exists(destination))
            throw new IOException("Workspace đã có dự án trùng tên.");
        Directory.CreateDirectory(ProjectsRoot);
        var staging = destination + ".migrating-" + Guid.NewGuid().ToString("N");
        try
        {
            CopyDirectory(source, staging);
            var originals = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            var copies = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
            if (originals.Length != copies.Length)
                throw new IOException("Số lượng tệp sao chép không khớp.");
            foreach (var original in originals)
            {
                var relative = Path.GetRelativePath(source, original);
                var copy = Path.Combine(staging, relative);
                if (!File.Exists(copy) || new FileInfo(original).Length != new FileInfo(copy).Length)
                    throw new IOException("Không thể xác minh bản sao: " + relative);
            }
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    /// <summary>
    /// Imports an existing H2 project (including Terrain examples extracted
    /// from a ZIP) without modifying the original directory.
    /// </summary>
    public string ImportExternalProjectToWorkspace(string projectDirectory)
    {
        var source = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "config.json")))
            throw new FileNotFoundException("Thư mục được chọn chưa có config.json hợp lệ.");
        var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
        ValidateName(name);
        ValidateRoots(Settings);
        var destination = InRoot(ProjectsRoot, name);
        if (Directory.Exists(destination))
            throw new IOException("Workspace đã có dự án '" + name + "'. Không ghi đè.");
        var staging = destination + ".importing-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(ProjectsRoot);
        try
        {
            CopyDirectory(source, staging);
            if (!File.Exists(Path.Combine(staging, "config.json")))
                throw new IOException("Bản sao dự án thiếu config.json.");
            var originals = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            var copies = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
            if (originals.Length != copies.Length)
                throw new IOException("Số tệp dự án không trùng khớp.");
            foreach (var original in originals)
            {
                var relative = Path.GetRelativePath(source, original);
                var copied = Path.Combine(staging, relative);
                if (!File.Exists(copied) || new FileInfo(original).Length != new FileInfo(copied).Length)
                    throw new IOException("Bản sao dự án khác bản gốc: " + relative);
            }
            Directory.Move(staging, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }

    public static JsonNode ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path)) ?? throw new JsonException("Tệp JSON trống.");

    public static void SaveJson(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, node.ToJsonString(JsonOptions.Pretty), new System.Text.UTF8Encoding(false));
        File.Move(tmp, path, true);
    }

    public static void SaveText(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents, new System.Text.UTF8Encoding(false));
        File.Move(tmp, path, true);
    }
}
