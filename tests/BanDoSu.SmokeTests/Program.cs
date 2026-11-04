using BanDoSu.Core;
using System.Text.Json.Nodes;

var root = Path.Combine(Path.GetTempPath(), "bandosu-tests-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(Path.Combine(root, "projects", "sample", "story"));
    var settings = new StudioSettings { StudioRoot = root };
    var projects = new ProjectFiles(settings);
    var file = projects.FileInProject("sample", "story", "shots.json");
    ProjectFiles.SaveText(Path.Combine(root, "projects", "sample", "config.json"), "{\"title\":\"Sample\"}");
    ProjectFiles.SaveText(file, """
    {"duration":10,"extension":"preserve","shots":[
    {"id":"s001","start":0,"end":5,"line":"Hello","unknown":42},
    {"id":"s002","start":5,"end":10,"line":"World"}]}
    """);

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
    Check(projects.List().Count == 1, "project discovery");

    var rejected = false;
    try { projects.ResolveProject("../escape"); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "path traversal rejected");
    Check(StudioCommands.ProjectArg("sample") == "projects/sample", "project CLI argument");
    Console.WriteLine("PASS: 8 smoke assertions.");
}
finally
{
    try { Directory.Delete(root, recursive:true); } catch (IOException) { }
}

static void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
