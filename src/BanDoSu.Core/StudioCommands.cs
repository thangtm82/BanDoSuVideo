namespace BanDoSu.Core;

public sealed class StudioCommands
{
    public StudioSettings Settings { get; }
    private readonly StudioProcess _runner = new();
    private readonly ProjectFiles _projects;
    public StudioCommands(StudioSettings settings)
    {
        Settings = settings;
        _projects = new ProjectFiles(settings);
    }

    public Task<ProcessResult> RunNodeAsync(IEnumerable<string> arguments, Action<string>? log = null, CancellationToken ct = default)
        => _runner.RunAsync(Settings.NodeExecutable, arguments, Settings.StudioRoot, log, ct);

    public Task<ProcessResult> RunPythonAsync(IEnumerable<string> arguments, Action<string>? log = null, CancellationToken ct = default)
        => _runner.RunAsync(Settings.PythonExecutable, arguments, Settings.StudioRoot, log, ct);

    public Task<ProcessResult> DoctorAsync(Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/doctor.py"], log, ct);

    public async Task<ProcessResult> CreateProjectAsync(string slug, string title, bool captions, Action<string>? log = null, CancellationToken ct = default)
    {
        ProjectFiles.ValidateName(slug);
        ProjectFiles.ValidateRoots(Settings);
        if (_projects.Exists(slug)) throw new IOException("Tên dự án đã tồn tại trong Workspace hoặc Engine.");
        var a = new List<string> { "tools/new_project.py", "--name", slug, "--title", title, "--pack", "bando", "--lang", "vi", "--style", "flat" };
        if (captions) a.Add("--captions");
        var result = await RunPythonAsync(a, log, ct).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            var path = _projects.MoveNewProjectToWorkspace(slug);
            log?.Invoke("Đã chuyển dự án vào Workspace: " + path);
        }
        return result;
    }

    public Task<ProcessResult> PrepareVoiceAsync(string project, string voiceDir, string lines, Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/prep_voice.py", "--project", ProjectArgument(project), "--voice-dir", voiceDir, "--lines", lines, "--lang", "vi"], log, ct);

    public Task<ProcessResult> DraftShotsAsync(string project, Action<string>? log = null, CancellationToken ct = default)
        => RunPythonAsync(["tools/draft_shots.py", "--project", ProjectArgument(project), "--min", "3", "--max", "7.5"], log, ct);

    public Task<ProcessResult> RenderAsync(string project, RenderMode mode, Action<string>? log = null, CancellationToken ct = default)
    {
        var args = new List<string> { "render.mjs", "--project", ProjectArgument(project) };
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
        => RunPythonAsync(["tools/mix.py", "--project", ProjectArgument(project)], log, ct);

    private string ProjectArgument(string project)
    {
        var directory = _projects.ResolveProject(project);
        if (!File.Exists(Path.Combine(directory, "config.json")))
            throw new FileNotFoundException("Không tìm thấy cấu hình dự án.", Path.Combine(directory, "config.json"));
        // The H2 CLI receives an absolute project path for workspace projects.
        // Legacy projects retain their original relative path for compatibility.
        if (directory.StartsWith(_projects.LegacyProjectsRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) return "projects/" + project;
        return directory;
    }

    public static string ProjectArg(string project) => "projects/" + project;
}

public enum RenderMode { Check, Sheet, Preview, Full, Thumbnail }
