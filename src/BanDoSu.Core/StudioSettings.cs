using System.Text.Json;

namespace BanDoSu.Core;

public sealed class StudioSettings
{
    // Defaults are computed from the location of the executable. Existing saved
    // absolute paths remain supported until the user selects Portable mode.
    public string StudioRoot { get; set; } = new PortableStudioPaths().Engine;
    public string WorkspaceRoot { get; set; } = new PortableStudioPaths().Projects;
    public string ExportRoot { get; set; } = new PortableStudioPaths().Exports;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPortable
    {
        get
        {
            var paths = new PortableStudioPaths();
            return SamePath(StudioRoot, paths.Engine) &&
                SamePath(WorkspaceRoot, paths.Projects) &&
                SamePath(ExportRoot, paths.Exports);
        }
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    public void UsePortablePaths()
    {
        var paths = new PortableStudioPaths();
        if (!SamePath(StudioRoot, paths.Engine) && !string.IsNullOrWhiteSpace(StudioRoot))
            LegacyEngineRoot = StudioRoot;
        StudioRoot = paths.Engine;
        WorkspaceRoot = paths.Projects;
        ExportRoot = paths.Exports;
    }
    // Old external engine is remembered for non-destructive migration after switching.
    public string? LegacyEngineRoot { get; set; }

    public string NodeExecutable { get; set; } = "node";
    public string PythonExecutable { get; set; } = "python";
    public string FfmpegExecutable { get; set; } = "ffmpeg";

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BanDoSuVideo", "settings.json");

    public static StudioSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch (JsonException) { }
        catch (IOException) { }
        return new();
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions.Pretty));
        File.Move(tmp, SettingsPath, true);
    }
}

public static class JsonOptions
{
    public static JsonSerializerOptions Pretty => new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
}
