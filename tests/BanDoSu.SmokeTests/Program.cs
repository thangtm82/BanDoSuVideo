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
