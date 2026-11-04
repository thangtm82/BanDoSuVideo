namespace BanDoSu.Core;

public sealed class StudioCommands
{
    public StudioSettings Settings { get; }
    private readonly StudioProcess _runner = new();
    private readonly ProjectFiles _projects;
    private readonly EngineCatalog _engines;

    public StudioCommands(StudioSettings settings)
    {
        Settings = settings;
        _projects = new ProjectFiles(settings);
        _engines = new EngineCatalog(settings);
    }

    private string PythonFor(string engineRoot)
    {
        var privatePython = Path.Combine(engineRoot, ".venv", "Scripts", "python.exe");
        return File.Exists(privatePython) ? privatePython : Settings.PythonExecutable;
    }

    private Task<ProcessResult> Python(string root, IEnumerable<string> arguments,
        Action<string>? log, CancellationToken ct) =>
        _runner.RunAsync(PythonFor(root), arguments, root, log, ct);

    private Task<ProcessResult> Node(string root, IEnumerable<string> arguments,
        Action<string>? log, CancellationToken ct) =>
        _runner.RunAsync(Settings.NodeExecutable, arguments, root, log, ct);

    public Task<ProcessResult> RunNodeAsync(IEnumerable<string> arguments,
        Action<string>? log = null, CancellationToken ct = default) =>
        Node(Settings.StudioRoot, arguments, log, ct);

    public Task<ProcessResult> RunPythonAsync(IEnumerable<string> arguments,
        Action<string>? log = null, CancellationToken ct = default) =>
        Python(Settings.StudioRoot, arguments, log, ct);

    public Task<ProcessResult> DoctorAsync(Action<string>? log = null, CancellationToken ct = default,
        string? engineRoot = null) =>
        Python(engineRoot ?? Settings.StudioRoot, ["tools/doctor.py"], log, ct);

    public async Task<ProcessResult> CreateProjectAsync(string slug, string title,
        bool captions, PackOption profile, Action<string>? log = null,
        CancellationToken ct = default)
    {
        ProjectFiles.ValidateName(slug);
        ProjectFiles.ValidateRoots(Settings);
        if (_projects.Exists(slug))
            throw new IOException("Tên dự án đã tồn tại trong Workspace hoặc Engine.");
        if (!_engines.Discover().Any(x => x.Pack == profile.Pack &&
                x.EngineId == profile.EngineId &&
                x.EngineRoot.Equals(profile.EngineRoot, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Pack hoặc phiên bản Engine không có trong danh mục.");

        var args = new List<string> { "tools/new_project.py",
            "--name", slug, "--title", title,
            "--pack", profile.Pack, "--lang", profile.Language,
            "--style", profile.Style };
        if (captions) args.Add("--captions");
        var result = await Python(profile.EngineRoot, args, log, ct).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            var path = _projects.MoveNewProjectToWorkspace(slug, profile.EngineRoot);
            EngineCatalog.WriteBinding(path, profile);
            log?.Invoke("Đã tạo dự án pack '" + profile.Pack +
                "' bằng engine '" + profile.EngineId + "': " + path);
        }
        return result;
    }

    // Backward-compatible overload: when only legacy UI calls it, use the default bando pack.
    public Task<ProcessResult> CreateProjectAsync(string slug, string title, bool captions,
        Action<string>? log = null, CancellationToken ct = default)
    {
        var profile = _engines.Discover().FirstOrDefault(x => x.Pack == "bando") ??
            throw new InvalidOperationException("Chưa cài engine hỗ trợ bando.");
        return CreateProjectAsync(slug, title, captions, profile, log, ct);
    }

    private (string Root, string ProjectPath) ForProject(string name)
    {
        var directory = _projects.ResolveProject(name);
        if (!File.Exists(Path.Combine(directory, "config.json")))
            throw new FileNotFoundException("Không tìm thấy config.json của dự án.");
        var engine = _engines.ResolveForProject(directory);
        var engineProjects = Path.GetFullPath(Path.Combine(engine.EngineRoot, "projects"));
        var relative = directory.StartsWith(engineProjects + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) ? "projects/" + name : directory;
        return (engine.EngineRoot, relative);
    }

    public Task<ProcessResult> PrepareVoiceAsync(string project, string voiceDir, string lines,
        Action<string>? log = null, CancellationToken ct = default)
    {
        var ctx = ForProject(project);
        return Python(ctx.Root, ["tools/prep_voice.py", "--project", ctx.ProjectPath,
            "--voice-dir", voiceDir, "--lines", lines, "--lang", _engines.ResolveForProject(
                _projects.ResolveProject(project)).Language], log, ct);
    }

    public Task<ProcessResult> DraftShotsAsync(string project,
        Action<string>? log = null, CancellationToken ct = default)
    {
        var ctx = ForProject(project);
        return Python(ctx.Root, ["tools/draft_shots.py", "--project", ctx.ProjectPath,
            "--min", "3", "--max", "7.5"], log, ct);
    }

    public Task<ProcessResult> RenderAsync(string project, RenderMode mode,
        Action<string>? log = null, CancellationToken ct = default)
    {
        var ctx = ForProject(project);
        var args = new List<string> { "render.mjs", "--project", ctx.ProjectPath };
        switch (mode)
        {
            case RenderMode.Check: args.AddRange(["--check", "--scale", "0.25"]); break;
            case RenderMode.Sheet: args.AddRange(["--sheet", "--scale", "0.4", "--at", "0.85"]); break;
            case RenderMode.Preview: args.AddRange(["--from", "0", "--to", "30", "--scale", "0.5"]); break;
            case RenderMode.Thumbnail: args.Add("--thumb"); break;
        }
        return Node(ctx.Root, args, log, ct);
    }

    public Task<ProcessResult> MixAsync(string project,
        Action<string>? log = null, CancellationToken ct = default)
    {
        var ctx = ForProject(project);
        return Python(ctx.Root, ["tools/mix.py", "--project", ctx.ProjectPath], log, ct);
    }

    public static string ProjectArg(string project) => "projects/" + project;
}

public enum RenderMode { Check, Sheet, Preview, Full, Thumbnail }
