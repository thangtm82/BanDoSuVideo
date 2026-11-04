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

    // npm/npx regression: never resolve a CLI script relative to StudioRoot.
    var nodeFixture = Path.Combine(root, "node-install");
    var npmBin = Path.Combine(nodeFixture, "node_modules", "npm", "bin");
    Directory.CreateDirectory(npmBin);
    ProjectFiles.SaveText(Path.Combine(nodeFixture, "node.exe"), "stub executable");
    ProjectFiles.SaveText(Path.Combine(npmBin, "npm-cli.js"), "// stub npm");
    ProjectFiles.SaveText(Path.Combine(npmBin, "npx-cli.js"), "// stub npx");
    var isolatedStudio = Path.Combine(root, "studio-npx-regression");
    Directory.CreateDirectory(Path.Combine(isolatedStudio, "node_modules", "npm", "bin"));
    var nodeTools = new NodePackageTools(Path.Combine(nodeFixture, "node.exe"), isolatedStudio);
    Check(nodeTools.ResolveNpmCli() == Path.Combine(npmBin, "npm-cli.js"),
        "npm entrypoint resolved from Node installation");
    Check(nodeTools.ResolveNpmCli(true) == Path.Combine(npmBin, "npx-cli.js"),
        "npx entrypoint resolved from Node installation");
    Check(!nodeTools.ResolveNpmCli(true).StartsWith(isolatedStudio, StringComparison.OrdinalIgnoreCase),
        "npx never resolved from H2 studio");
    var missingPlaywright = false;
    try { nodeTools.ResolvePlaywrightCli(); }
    catch (FileNotFoundException) { missingPlaywright = true; }
    Check(missingPlaywright, "missing local Playwright reports actionable error");
    ProjectFiles.SaveText(Path.Combine(isolatedStudio, "node_modules", "playwright", "cli.js"), "// stub");
    Check(nodeTools.ResolvePlaywrightCli() == Path.Combine(isolatedStudio, "node_modules", "playwright", "cli.js"),
        "Chromium setup invokes engine-installed Playwright");
    File.Delete(Path.Combine(npmBin, "npx-cli.js"));
    var missingNpx = false;
    try { nodeTools.ResolveNpmCli(true); }
    catch (FileNotFoundException ex) { missingNpx = ex.Message.Contains("sửa chữa/cài lại Node.js LTS"); }
    Check(missingNpx, "missing npx JS entrypoint produces clear repair guidance");

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
    // OpenAI-compatible URL normalization and validation.
    Check(AiEndpoint.Resolve("http://127.0.0.1:8080/v1", AiCustomEndpoint.ChatCompletions)
        .ToString() == "http://127.0.0.1:8080/v1/chat/completions",
        "Custom chat endpoint from /v1");
    Check(AiEndpoint.Resolve("http://localhost:8080/v1/chat/completions",
        AiCustomEndpoint.Responses).AbsolutePath == "/v1/responses",
        "Custom endpoint switch does not append twice");
    Check(AiEndpoint.Resolve("https://proxy.example.com/tenant/v1/responses",
        AiCustomEndpoint.ChatCompletions).AbsolutePath == "/tenant/v1/chat/completions",
        "Custom endpoint preserves proxy prefix");
    var invalidUrl = false;
    try { AiEndpoint.Resolve("file:///etc/passwd", AiCustomEndpoint.Responses); }
    catch (ArgumentException) { invalidUrl = true; }
    Check(invalidUrl, "Custom URL rejects unsupported protocols");

    var customSettings = new StudioSettings
    {
        CustomAiBaseUrl = "http://127.0.0.1:8080/v1",
        CustomAiModel = "local-model",
        CustomAiEndpoint = AiCustomEndpoint.Responses
    };
    var serializedSettings = System.Text.Json.JsonSerializer.Serialize(customSettings);
    Check(!serializedSettings.Contains("secret-gemini-key"), "settings do not accidentally serialize test key");
    var restoredSettings = System.Text.Json.JsonSerializer.Deserialize<StudioSettings>(serializedSettings)!;
    Check(restoredSettings.CustomAiModel == "local-model" &&
        restoredSettings.CustomAiEndpoint == AiCustomEndpoint.Responses, "Custom preferences round-trip");

    var customChat = new FakeCustomHandler(generatedScript, AiCustomEndpoint.ChatCompletions, false);
    using (var http = new HttpClient(customChat))
    {
        var ai = new AiScriptStudio(skillFixture, http);
        var request = new AiScriptRequest(AiScriptProvider.Custom, "local-coder",
            "", "Vì sao Hoa Lư chuyển về Thăng Long?", 12,
            "http://127.0.0.1:8080/v1", AiCustomEndpoint.ChatCompletions);
        var connection = ai.TestConnectionAsync(request).GetAwaiter().GetResult();
        Check(connection.Contains("Kết nối thành công"), "Custom chat Test Connection succeeds");
        var output = ai.GenerateAsync(request, "hoa-lu-custom-chat").GetAwaiter().GetResult();
        Check(AiScriptStudio.EquivalentNarration(output.Script, output.Voice), "Custom chat script/voice parity");
        Check(customChat.CallCount == 4, "Custom chat uses one probe and three generation requests");
        Check(customChat.AllRequestsValid, "Custom chat request schema, target and optional no-auth");
    }
    var customResponses = new FakeCustomHandler(generatedScript, AiCustomEndpoint.Responses, true);
    using (var http = new HttpClient(customResponses))
    {
        var ai = new AiScriptStudio(skillFixture, http);
        var request = new AiScriptRequest(AiScriptProvider.Custom, "proxy-model",
            "sample-api-key", "Vì sao Hoa Lư chuyển về Thăng Long?", 12,
            "https://proxy.example.com/tenant/v1", AiCustomEndpoint.Responses);
        var connection = ai.TestConnectionAsync(request).GetAwaiter().GetResult();
        Check(connection.Contains("Kết nối thành công"), "Custom Responses Test Connection succeeds");
        var output = ai.GenerateAsync(request, "hoa-lu-custom-responses").GetAwaiter().GetResult();
        Check(output.VoiceSegments > 5, "Custom Responses generation parsed");
        Check(customResponses.CallCount == 4 && customResponses.AllRequestsValid,
            "Custom Responses request schema, bearer token and URL prefix");
    }
    var windowXamlPath = Path.Combine(Environment.CurrentDirectory,
        "src", "BanDoSu.Desktop", "MainWindow.xaml");
    var windowXaml = System.Xml.Linq.XDocument.Load(windowXamlPath);
    System.Xml.Linq.XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    System.Xml.Linq.XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    var tabPanel = windowXaml.Descendants(wpf + "TabControl")
        .First(x => (string?)x.Attribute(xaml + "Name") == "MainTabs");
    var tabOrder = tabPanel.Elements(wpf + "TabItem")
        .Select(x => (string?)x.Attribute("Header")).ToArray();
    Check(tabOrder.Length >= 8 &&
          tabOrder[0] == "Dự án" &&
          tabOrder[1] == "AI Kịch bản" &&
          tabOrder[2] == "Kịch bản và giọng đọc" &&
          tabOrder[6] == "Cài đặt" &&
          tabOrder[7] == "Quản lý môi trường",
        "WPF main tabs follow project → AI script → voice → editor sequence");

    // TTS preparation works without a provider, but must never generate silent/fake MP3.
    var ttsPieces = TtsBatch.ParseSegments("Hoa Lư là kinh đô. | Năm 1010 dời đô về Thăng Long.");
    Check(ttsPieces.Count == 2 && ttsPieces[0].FileName == "1.mp3" &&
          ttsPieces[1].FileName == "2.mp3", "TTS splits voice into sequential MP3 filenames");
    Check(ttsPieces[1].Text == "Năm 1010 dời đô về Thăng Long.",
        "TTS does not change the narrated segment text");
    var blankSegmentRejected = false;
    try { TtsBatch.ParseSegments("Đoạn một||Đoạn ba"); }
    catch (InvalidDataException) { blankSegmentRejected = true; }
    Check(blankSegmentRejected, "TTS rejects empty voice segments");
    var missingVoiceRejected = false;
    try { TtsBatch.RequireVoiceId("  "); }
    catch (InvalidDataException) { missingVoiceRejected = true; }
    Check(missingVoiceRejected, "TTS requires voice selection");
    var ttsOutput = Path.Combine(root, "tts-output");
    var noProviderRejected = false;
    try
    {
        TtsBatch.GenerateAsync("Một đoạn thử nghiệm.", "voice-a", ttsOutput, null)
            .GetAwaiter().GetResult();
    }
    catch (NotSupportedException) { noProviderRejected = true; }
    Check(noProviderRejected && !Directory.Exists(ttsOutput),
        "TTS without API does not create files or folders");
    var fakeTts = new FakeTtsProvider();
    var written = TtsBatch.GenerateAsync(
        "Đoạn thứ nhất. | Đoạn thứ hai.", "voice-a", ttsOutput, fakeTts)
        .GetAwaiter().GetResult();
    Check(written == 2 && fakeTts.Calls == 2, "TTS adapter called once per segment");
    Check(File.Exists(Path.Combine(ttsOutput, "1.mp3")) &&
          File.Exists(Path.Combine(ttsOutput, "2.mp3")), "TTS produces correctly numbered MP3 paths");
    Check(!Directory.EnumerateFiles(ttsOutput, "*.tmp").Any(), "TTS temp files cleaned");
    var overwriteBlocked = false;
    try { TtsBatch.GenerateAsync("Đoạn khác.", "voice-a", ttsOutput, fakeTts).GetAwaiter().GetResult(); }
    catch (IOException) { overwriteBlocked = true; }
    Check(overwriteBlocked, "TTS refuses overwriting previous MP3");
    var settingsVoices = new StudioSettings
    {
        TtsVoiceIds = ["giọng-nam", "giọng-nữ"],
        TtsSelectedVoiceId = "giọng-nữ"
    };
    var restoredVoices = System.Text.Json.JsonSerializer.Deserialize<StudioSettings>(
        System.Text.Json.JsonSerializer.Serialize(settingsVoices))!;
    Check(restoredVoices.TtsVoiceIds.SequenceEqual(settingsVoices.TtsVoiceIds) &&
          restoredVoices.TtsSelectedVoiceId == "giọng-nữ", "TTS voice preferences round-trip");

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

sealed class FakeCustomHandler : HttpMessageHandler
{
    private readonly string _script;
    private readonly AiCustomEndpoint _endpoint;
    private readonly bool _authorized;
    public int CallCount { get; private set; }
    public bool AllRequestsValid { get; private set; } = true;

    public FakeCustomHandler(string script, AiCustomEndpoint endpoint, bool authorized)
    {
        _script = script;
        _endpoint = endpoint;
        _authorized = authorized;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var path = request.RequestUri!.AbsolutePath;
        AllRequestsValid &= _endpoint == AiCustomEndpoint.ChatCompletions
            ? path == "/v1/chat/completions" : path == "/tenant/v1/responses";
        AllRequestsValid &= !request.RequestUri.ToString().Contains("sample-api-key");
        AllRequestsValid &= _authorized
            ? request.Headers.Authorization?.Scheme == "Bearer" &&
              request.Headers.Authorization.Parameter == "sample-api-key"
            : request.Headers.Authorization is null;

        using var json = System.Text.Json.JsonDocument.Parse(
            await request.Content!.ReadAsStringAsync(cancellationToken));
        var body = json.RootElement;
        var prompt = _endpoint == AiCustomEndpoint.ChatCompletions
            ? body.GetProperty("messages")[0].GetProperty("content").GetString()!
            : body.GetProperty("input").GetString()!;
        AllRequestsValid &= body.GetProperty("model").GetString() == (_authorized ? "proxy-model" : "local-coder");
        AllRequestsValid &= _endpoint == AiCustomEndpoint.ChatCompletions
            ? body.TryGetProperty("max_tokens", out _) &&
              !body.TryGetProperty("max_output_tokens", out _)
            : body.TryGetProperty("max_output_tokens", out _) &&
              !body.TryGetProperty("max_tokens", out _);
        var answer = prompt.Contains("NHIỆM VỤ 2:", StringComparison.Ordinal)
            ? "# Bản đồ\nCẦN KIỂM CHỨNG"
            : prompt.Contains("NHIỆM VỤ 3:", StringComparison.Ordinal)
                ? "# Gói YouTube\nCẦN KIỂM CHỨNG" : _script;
        if (prompt.Contains("single word OK", StringComparison.Ordinal))
            answer = "OK";
        var response = _endpoint == AiCustomEndpoint.ChatCompletions
            ? System.Text.Json.JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { role = "assistant", content = answer } } }
            })
            : System.Text.Json.JsonSerializer.Serialize(new
            {
                output = new[] { new { content = new[] { new { type = "output_text", text = answer } } } }
            });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}

sealed class FakeTtsProvider : ITtsSynthesisProvider
{
    public int Calls { get; private set; }
    public Task<byte[]> SynthesizeMp3Async(string text, string voiceId, CancellationToken cancellationToken)
    {
        Calls++;
        if (voiceId != "voice-a" || string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("TTS adapter received incorrect segment data.");
        // Test fixture: ID3 signature only. Not used for real audio playback.
        return Task.FromResult(new byte[] { 0x49, 0x44, 0x33, 0x03, 0x00, 0x00 });
    }
}
