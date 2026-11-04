using System.Text.Json;
using System.Text.Json.Nodes;

namespace BanDoSu.Core;

public sealed record ProjectSummary(string Name, string Directory, string Title);

public sealed class ProjectFiles
{
    public StudioSettings Settings { get; }
    public ProjectFiles(StudioSettings settings) => Settings = settings;

    public string ProjectsRoot => Path.Combine(Settings.StudioRoot, "projects");

    public string ResolveProject(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains('/') || name.Contains('\\') || name.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid project name.", nameof(name));
        var root = Path.GetFullPath(ProjectsRoot) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(ProjectsRoot, name));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Project path is outside the Studio root.");
        return candidate;
    }

    public IReadOnlyList<ProjectSummary> List()
    {
        if (!Directory.Exists(ProjectsRoot)) return Array.Empty<ProjectSummary>();
        return Directory.GetDirectories(ProjectsRoot)
            .Where(p => File.Exists(Path.Combine(p, "config.json")))
            .Select(p =>
            {
                var title = Path.GetFileName(p);
                try { title = ReadJson(Path.Combine(p, "config.json"))["title"]?.GetValue<string>() ?? title; }
                catch (Exception ex) when (ex is JsonException or IOException) { }
                return new ProjectSummary(Path.GetFileName(p), p, title);
            }).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public string FileInProject(string project, params string[] parts)
    {
        var root = ResolveProject(project);
        var full = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path escapes project folder.");
        return full;
    }

    public static JsonNode ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path)) ?? throw new JsonException("Empty JSON.");

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
