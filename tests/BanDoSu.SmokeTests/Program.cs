using BanDoSu.Core;

var root = Path.Combine(Path.GetTempPath(), "bandosu-tests-" + Guid.NewGuid().ToString("N"));
var engine = Path.Combine(root, "engine");
var workspace = Path.Combine(root, "workspace");
var exports = Path.Combine(root, "exports");

try
{
    Directory.CreateDirectory(Path.Combine(engine, "projects", "legacy", "story"));
    Directory.CreateDirectory(workspace);
    var settings = new StudioSettings { StudioRoot = engine, WorkspaceRoot = workspace, ExportRoot = exports };
    ProjectFiles.ValidateRoots(settings);
    var projects = new ProjectFiles(settings);
    var legacyConfig = Path.Combine(engine, "projects", "legacy", "config.json");
    ProjectFiles.SaveText(legacyConfig, "{\"title\":\"Dự án cũ\"}");
    Check(projects.ResolveProject("legacy") == Path.Combine(engine, "projects", "legacy"), "legacy project path");

    var staging = Path.Combine(engine, "projects", "sample");
    Directory.CreateDirectory(Path.Combine(staging, "story"));
    ProjectFiles.SaveText(Path.Combine(staging, "config.json"), "{\"title\":\"Sample\"}");
    ProjectFiles.SaveText(Path.Combine(staging, "story", "shots.json"), """
    {"duration":10,"extension":"preserve","shots":[
    {"id":"s001","start":0,"end":5,"line":"Hello","unknown":42},
    {"id":"s002","start":5,"end":10,"line":"World"}]}
    """);
    Check(projects.Exists("sample"), "new project collision check");
    var moved = projects.MoveNewProjectToWorkspace("sample");
    Check(moved == Path.Combine(workspace, "sample"), "workspace migration location");
    Check(!Directory.Exists(staging), "engine staging removed");
    Check(projects.ResolveProject("sample") == moved, "workspace project resolution");

    var file = projects.FileInProject("sample", "story", "shots.json");
    var doc = new ShotDocument(ProjectFiles.ReadJson(file));
    doc.SetInitialMapDefaults();
    doc.UpdateArgs(1, """{"view":"dong-bang-song-hong","year":"1010"}""");
    doc.Validate();
    ProjectFiles.SaveJson(file, doc.Root);
    var restored = ProjectFiles.ReadJson(file);
    Check(restored["extension"]?.ToString() == "preserve", "unknown root field");
    Check(restored["shots"]?[0]?["unknown"]?.ToString() == "42", "unknown scene field");
    Check(restored["shots"]?[0]?["tpl"]?.ToString() == "map", "default template");
    Check(restored["shots"]?[0]?["args"]?["view"]?.ToString() == "viet-nam", "camera defaults");
    Check(restored["shots"]?[1]?["args"]?["year"]?.ToString() == "1010", "scene edit");
    Check(projects.List().Count == 2, "new and legacy project discovery");
    var copiedLegacy = projects.CopyLegacyProjectToWorkspace("legacy");
    Check(File.Exists(Path.Combine(copiedLegacy, "config.json")), "legacy copied into workspace");
    Check(File.Exists(legacyConfig), "legacy original retained");
    Check(projects.ResolveProject("legacy") == copiedLegacy, "workspace copy preferred after migration");
    Check(projects.List().First(p => p.Name == "legacy").IsLegacy == false, "migrated project listed as workspace");


    var rejected = false;
    try { projects.ResolveProject("../escape"); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "path traversal rejected");
    rejected = false;
    try { projects.FileInProject("sample", "..", "escape"); }
    catch (UnauthorizedAccessException) { rejected = true; }
    Check(rejected, "project file traversal rejected");

    ProjectFiles.SaveText(projects.FileInProject("sample", "out", "video.mp4"), "fake video");
    var published = new ExportPublisher(settings).Publish("sample");
    Check(File.Exists(Path.Combine(published, "video.mp4")), "export published");
    Check(File.Exists(projects.FileInProject("sample", "out", "video.mp4")), "source kept intact");
    var second = new ExportPublisher(settings).Publish("sample");
    Check(published != second, "unique export folders");

    rejected = false;
    try { ProjectFiles.ValidateRoots(new StudioSettings { StudioRoot = engine, WorkspaceRoot = Path.Combine(engine, "projects"), ExportRoot = exports }); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected, "workspace cannot overlap engine");

    Check(StudioCommands.ProjectArg("legacy") == "projects/legacy", "legacy CLI helper");
    // Environment check is read-only; no installers are run in smoke tests.
    var environment = new EnvironmentManager(settings);
    Check(EnvironmentManager.InstallationOrder.Count == 10, "environment dependency catalog");
    Check(EnvironmentManager.WinGetId(DependencyKind.Node) == "OpenJS.NodeJS.LTS", "official Node WinGet mapping");
    Check(EnvironmentManager.WinGetId(DependencyKind.PlaywrightChromium) is null, "Playwright installed per project");
    var missingEngine = environment.CheckAsync(DependencyKind.Engine).GetAwaiter().GetResult();
    Check(!missingEngine.Installed && !missingEngine.CanInstall, "missing engine is not auto-installed");
    ProjectFiles.SaveText(Path.Combine(engine, "render.mjs"), "// smoke");
    ProjectFiles.SaveText(Path.Combine(engine, "player.html"), "<html></html>");
    Directory.CreateDirectory(Path.Combine(engine, "tools"));
    Directory.CreateDirectory(Path.Combine(engine, "packs"));
    var validEngine = environment.CheckAsync(DependencyKind.Engine).GetAwaiter().GetResult();
    Check(validEngine.Installed, "engine detected after setup");
    if (OperatingSystem.IsWindows())
    {
        var commandFile = Path.Combine(root, "verify cmd script.cmd");
        File.WriteAllText(commandFile, "@echo off\r\necho environment-command-ok\r\n");
        var commandResult = new StudioProcess().RunAsync(commandFile, Array.Empty<string>(), root)
            .GetAwaiter().GetResult();
        Check(commandResult.ExitCode == 0 && commandResult.Output.Contains("environment-command-ok"),
            "Windows batch commands supported by process runner");
    }


    var portable = new PortableStudioPaths(Path.Combine(root, "portable-app"));
    portable.Initialize();
    Check(Directory.Exists(portable.Engine) && Directory.Exists(portable.Projects) &&
          Directory.Exists(portable.Exports), "portable sibling folders created");
    Check(!portable.HasEngine, "empty engine is not considered installed");

    var fixture = Path.Combine(root, "h2-source");
    Directory.CreateDirectory(Path.Combine(fixture, "tools"));
    Directory.CreateDirectory(Path.Combine(fixture, "packs"));
    Directory.CreateDirectory(Path.Combine(fixture, "projects", "user-data"));
    Directory.CreateDirectory(Path.Combine(fixture, "node_modules"));
    Directory.CreateDirectory(Path.Combine(fixture, ".venv"));
    ProjectFiles.SaveText(Path.Combine(fixture, "render.mjs"), "// test");
    ProjectFiles.SaveText(Path.Combine(fixture, "player.html"), "<html></html>");
    ProjectFiles.SaveText(Path.Combine(fixture, "tools", "doctor.py"), "# test");
    ProjectFiles.SaveText(Path.Combine(fixture, "packs", "pack.js"), "// test");
    ProjectFiles.SaveText(Path.Combine(fixture, "projects", "user-data", "config.json"), "{}");
    ProjectFiles.SaveText(Path.Combine(fixture, "node_modules", "placeholder.js"), "// test");
    ProjectFiles.SaveText(Path.Combine(fixture, ".venv", "ignore.txt"), "test");
    Check(PortableStudioPaths.IsValidEngine(fixture), "engine fixture is complete");
    portable.ImportEngineAsync(fixture).GetAwaiter().GetResult();
    Check(portable.HasEngine, "portable imported engine usable");
    Check(File.Exists(Path.Combine(fixture, "render.mjs")), "original engine unchanged");
    Check(!Directory.Exists(Path.Combine(portable.Engine, "projects")), "engine projects excluded from import");
    Check(!Directory.Exists(Path.Combine(portable.Engine, "node_modules")), "npm dependencies excluded from import");
    Check(!Directory.Exists(Path.Combine(portable.Engine, ".venv")), "Python venv excluded from import");
    Check(File.Exists(Path.Combine(portable.Engine, "tools", "doctor.py")), "engine tools imported");
    var importBlocked = false;
    try { portable.ImportEngineAsync(fixture).GetAwaiter().GetResult(); }
    catch (IOException) { importBlocked = true; }
    Check(importBlocked, "existing engine protected from overwrite");

    var portableSettings = new StudioSettings();
    portableSettings.UsePortablePaths();
    Check(portableSettings.IsPortable, "portable settings use EXE root");
    var externalConfig = new StudioSettings
    {
        StudioRoot = engine, WorkspaceRoot = portable.Projects, ExportRoot = portable.Exports,
        LegacyEngineRoot = engine
    };
    var oldProjects = new ProjectFiles(externalConfig);
    Check(oldProjects.List().Any(x => x.Name == "legacy"), "external engine project discovery");
    externalConfig.UsePortablePaths();
    Check(externalConfig.LegacyEngineRoot == engine, "switch remembers old engine");
    var afterSwitch = new ProjectFiles(externalConfig);
    Check(afterSwitch.List().Any(x => x.Name == "legacy" && x.IsLegacy), "old projects remain visible after switch");
    var previousWorkspace = Path.Combine(root, "previous-workspace");
    Directory.CreateDirectory(Path.Combine(previousWorkspace, "old-workspace-video"));
    ProjectFiles.SaveText(Path.Combine(previousWorkspace, "old-workspace-video", "config.json"), "{\"title\":\"Old workspace\"}");
    var workspaceSwitch = new StudioSettings
    {
        StudioRoot = engine, WorkspaceRoot = previousWorkspace, ExportRoot = exports
    };
    workspaceSwitch.UsePortablePaths();
    Check(workspaceSwitch.LegacyWorkspaceRoot == previousWorkspace, "old workspace remembered");
    var previousFiles = new ProjectFiles(workspaceSwitch);
    Check(previousFiles.List().Any(x => x.Name == "old-workspace-video" && x.IsLegacy),
        "previous workspace visible after switching to portable");

    var migrated = afterSwitch.CopyLegacyProjectToWorkspace("legacy");
    Check(File.Exists(Path.Combine(migrated, "config.json")), "legacy project copied into portable workspace");
    Check(File.Exists(legacyConfig), "legacy source preserved after portable migration");

    Console.WriteLine("PASS: all workspace/export smoke assertions.");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch (IOException) { }
}

static void Check(bool value, string name)
{
    if (!value) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
