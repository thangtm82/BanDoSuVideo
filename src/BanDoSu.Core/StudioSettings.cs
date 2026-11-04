using System.Text.Json;

namespace BanDoSu.Core;

public sealed class StudioSettings
{
    public string StudioRoot { get; set; } = @"D:\BanDoSu\h2-2d-studio";
    // Keep engine separate from user-created projects and published videos.
    // Existing settings.json files without these fields remain supported.
    public string WorkspaceRoot { get; set; } = @"D:\BanDoSu\Projects";
    public string ExportRoot { get; set; } = @"D:\BanDoSu\Exports";
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
