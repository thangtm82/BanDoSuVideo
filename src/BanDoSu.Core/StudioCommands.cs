namespace BanDoSu.Core;

public sealed class StudioCommands
{
    public StudioSettings Settings { get; }
    private readonly StudioProcess _runner = new();
    public StudioCommands(StudioSettings settings) => Settings = settings;

    public Task<ProcessResult> RunNodeAsync(IEnumerable<string> arguments, Action<string>? log = null, CancellationToken ct = default)
        => _runner.RunAsync(Settings.NodeExecutable, arguments, Settings.StudioRoot, log, ct);

    public Task<ProcessResult> RunPythonAsync(IEnumerable<string> arguments, Action<string>? log = null, CancellationToken ct = default)
        => _runner.RunAsync(Settings.PythonExecutable, arguments, Settings.StudioRoot, log, ct);

    public Task<ProcessResult> DoctorAsync(Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/doctor.py"], log, ct);

    public Task<ProcessResult> CreateProjectAsync(string slug, string title, bool captions, Action<string>? log = null, CancellationToken ct = default)
    {
        var a = new List<string> { "tools/new_project.py", "--name", slug, "--title", title, "--pack", "bando", "--lang", "vi", "--style", "flat" };
        if (captions) a.Add("--captions");
        return RunPythonAsync(a, log, ct);
    }

    public Task<ProcessResult> PrepareVoiceAsync(string project, string voiceDir, string lines, Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/prep_voice.py", "--project", ProjectArg(project), "--voice-dir", voiceDir, "--lines", lines, "--lang", "vi"], log, ct);

    public Task<ProcessResult> DraftShotsAsync(string project, Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/draft_shots.py", "--project", ProjectArg(project), "--min", "3", "--max", "7.5"], log, ct);

    public Task<ProcessResult> RenderAsync(string project, RenderMode mode, Action<string>? log = null, CancellationToken ct = default)
    {
        var args = new List<string> { "render.mjs", "--project", ProjectArg(project) };
        switch (mode)
        {
            case RenderMode.Check: args.AddRange(["--check", "--scale", "0.25"]); break;
            case RenderMode.Sheet: args.AddRange(["--sheet", "--scale", "0.4", "--at", "0.85"]); break;
            case RenderMode.Preview: args.AddRange(["--from", "0", "--to", "30", "--scale", "0.5"]); break;
            case RenderMode.Thumbnail: args.Add("--thumb"); break;
        }
        return RunNodeAsync(args, log, ct);
    }

    public Task<ProcessResult> MixAsync(string project, Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/mix.py", "--project", ProjectArg(project)], log, ct);

    public static string ProjectArg(string project) => "projects/" + project;
}

public enum RenderMode { Check, Sheet, Preview, Full, Thumbnail }
