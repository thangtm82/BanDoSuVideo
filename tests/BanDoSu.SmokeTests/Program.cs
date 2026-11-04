using System.Net;
using System.Net.Http;
using System.Text;
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

    var skillFixture = Path.Combine(root, "skills", "kich-ban-bandosu");
    Directory.CreateDirectory(Path.Combine(skillFixture, "references"));
    ProjectFiles.SaveText(Path.Combine(skillFixture, "SKILL.md"), "Viết kịch bản Bản Đồ Sử.");
    ProjectFiles.SaveText(Path.Combine(skillFixture, "references", "cau-truc.md"), "Kể chuyện bảy nhịp.");
    ProjectFiles.SaveText(Path.Combine(skillFixture, "references", "dia-danh.md"), "hoa-lu, thang-long");
    var generatedScript = string.Join(" ", Enumerable.Repeat("Chúng ta nhìn về vùng đất Hoa Lư và Thăng Long.", 65));
    var (voiceTest, segmentCount) = AiScriptStudio.MakeVoice(generatedScript);
    Check(segmentCount > 5, "script automatically split into voice segments");
    Check(AiScriptStudio.EquivalentNarration(generatedScript, voiceTest), "voice text identical to script");
    Check(voiceTest.Split('|').All(x => AiScriptStudio.CountWords(x) <= 55),
        "voice segments respect maximum length");
    var corruptedVoice = voiceTest.Replace("Hoa Lư", "Hà Nội");
    Check(!AiScriptStudio.EquivalentNarration(generatedScript, corruptedVoice),
        "voice validation rejects different transcript");
    var openAiHandler = new FakeAiHandler(generatedScript, gemini: false);
    using (var http = new HttpClient(openAiHandler))
    {
        var ai = new AiScriptStudio(skillFixture, http);
        var output = ai.GenerateAsync(new AiScriptRequest(AiScriptProvider.OpenAI,
            "test-model", "secret-test-token", "Vì sao Hoa Lư chuyển về Thăng Long?"),
            "hoa-lu-test").GetAwaiter().GetResult();
        Check(output.VoiceSegments == segmentCount, "OpenAI result split");
        Check(output.MapPlan.Contains("Bản đồ"), "map plan generated");
        Check(output.Package.Contains("YouTube"), "YouTube package generated");
        Check(openAiHandler.CallCount == 3, "OpenAI three-step generation");
        Check(openAiHandler.AllRequestsAuthorized, "OpenAI bearer token header");
        AiScriptStudio.ValidateBundle(output.Script, output.Voice, output.MapPlan, output.Package);
        Check(AiScriptStudio.EquivalentNarration(output.Script, output.Voice),
            "generated artifacts consistent");
    }
    var geminiHandler = new FakeAiHandler(generatedScript, gemini: true);
    using (var http = new HttpClient(geminiHandler))
    {
        var ai = new AiScriptStudio(skillFixture, http);
        var output = ai.GenerateAsync(new AiScriptRequest(AiScriptProvider.Gemini,
            "test-gemini", "secret-gemini-key", "Vì sao Hoa Lư chuyển về Thăng Long?"),
            "hoa-lu-test").GetAwaiter().GetResult();
        Check(output.VoiceSegments > 5, "Gemini response parsed");
        Check(geminiHandler.CallCount == 3, "Gemini three-step generation");
        Check(geminiHandler.AllRequestsAuthorized, "Gemini x-goog-api-key header, no URL secrets");
    }
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

sealed class FakeAiHandler : HttpMessageHandler
{
    private readonly string _script;
    private readonly bool _gemini;
    public int CallCount { get; private set; }
    public bool AllRequestsAuthorized { get; private set; } = true;

    public FakeAiHandler(string script, bool gemini)
    {
        _script = script;
        _gemini = gemini;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var token = _gemini ? "secret-gemini-key" : "secret-test-token";
        var header = _gemini
            ? request.Headers.TryGetValues("x-goog-api-key", out var values) &&
                values.Contains(token)
            : request.Headers.Authorization?.Scheme == "Bearer" &&
                request.Headers.Authorization.Parameter == token;
        AllRequestsAuthorized &= header && !request.RequestUri!.ToString().Contains(token);
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var parsed = System.Text.Json.JsonDocument.Parse(body);
        // JSON escapes Vietnamese characters by default. Inspect the actual prompt,
        // not its raw JSON representation.
        var prompt = _gemini
            ? parsed.RootElement.GetProperty("contents")[0].GetProperty("parts")[0]
                .GetProperty("text").GetString()!
            : parsed.RootElement.GetProperty("input").GetString()!;
        var answer = prompt.Contains("NHIỆM VỤ 2:", StringComparison.Ordinal) ? "# Bản đồ\\nCẦN KIỂM CHỨNG"
            : prompt.Contains("NHIỆM VỤ 3:", StringComparison.Ordinal) ? "# Gói YouTube\\nCẦN KIỂM CHỨNG"
            : _script;
        var response = _gemini
            ? System.Text.Json.JsonSerializer.Serialize(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = answer } } } } }
            })
            : System.Text.Json.JsonSerializer.Serialize(new
            {
                output = new[] { new { content = new[] { new { text = answer } } } }
            });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}
