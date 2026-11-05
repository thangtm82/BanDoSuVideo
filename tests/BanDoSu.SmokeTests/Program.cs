using System.IO.Compression;
using System.Text.Json.Nodes;
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
    Check(EnvironmentManager.InstallationOrder.Count == 11, "environment dependency catalog");
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

    // Multi-Pack: the default Bản Đồ Sử engine and imported Terrain engine
    // remain independent; future packs are discovered dynamically.
    var bandoPackDir = Path.Combine(engine, "packs", "bando");
    Directory.CreateDirectory(bandoPackDir);
    ProjectFiles.SaveText(Path.Combine(bandoPackDir, "pack.js"), "// default bando engine");
    var installations = Path.Combine(root, "isolated-installations");
    var catalog = new EngineCatalog(settings, installations);
    Check(catalog.Discover().Any(x => x.Pack == "bando" && x.EngineId == "default"),
        "Multi-Pack detects default Bản Đồ Sử engine");

    var zipPath = Path.Combine(root, "h2-terrain-test.zip");
    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        static void AddFile(ZipArchive z, string path, string content)
        {
            using var writer = new StreamWriter(z.CreateEntry(path).Open());
            writer.Write(content);
        }
        const string prefix = "skills/lam-video-terrain/kit/";
        AddFile(archive, prefix + "render.mjs", "// terrain renderer");
        AddFile(archive, prefix + "player.html", "<html></html>");
        AddFile(archive, prefix + "tools/new_project.py", "# fixture");
        AddFile(archive, prefix + "tools/doctor.py", "# fixture");
        AddFile(archive, prefix + "packs/terrain/pack.js", "// terrain pack");
        AddFile(archive, prefix + "projects/private-voice/1.mp3", "private data omitted");
        AddFile(archive, prefix + "node_modules/huge.txt", "npm dependency omitted");
        AddFile(archive, "skills/lam-video-terrain/SKILL.md", "Terrain video skill");
        AddFile(archive, "skills/lam-video-terrain/references/mau-canh.md", "Terrain visual examples");
        AddFile(archive, "skills/lam-video-terrain/references/phong-cach.md", "Terrain appearance rules");
        AddFile(archive, "skills/kich-ban-terrain/SKILL.md", "Terrain script skill");
        AddFile(archive, "skills/kich-ban-terrain/references/cau-truc.md", "9 nhịp Terrain");
    }
    var importedTerrain = catalog.ImportArchiveAsync(zipPath, "terrain-v1")
        .GetAwaiter().GetResult();
    Check(importedTerrain.Length == 1 &&
          importedTerrain[0].Pack == "terrain" &&
          importedTerrain[0].EngineId == "terrain-v1",
        "Multi-Pack discovers terrain pack from nested H2 ZIP");
    var terrainEngine = importedTerrain[0].EngineRoot;
    Check(File.Exists(Path.Combine(terrainEngine, "render.mjs")) &&
          File.Exists(Path.Combine(terrainEngine, "packs", "terrain", "pack.js")),
        "Terrain renderer is installed side by side");
    Check(!Directory.Exists(Path.Combine(terrainEngine, "projects")) &&
          !Directory.Exists(Path.Combine(terrainEngine, "node_modules")),
        "Multi-Pack importer skips user projects and npm caches");
    Check(File.Exists(Path.Combine(terrainEngine, "skills", "kich-ban-terrain", "SKILL.md")),
        "Terrain authoring skill imported from ZIP");
    Check(File.Exists(Path.Combine(terrainEngine, "skills", "lam-video-terrain",
        "references", "mau-canh.md")),
        "Terrain Scene Builder references imported next to the engine");
    Check(!Directory.Exists(Path.Combine(terrainEngine, "skills", "lam-video-terrain", "kit")),
        "Terrain authoring references do not duplicate bundled kit");
    Check(File.Exists(Path.Combine(engine, "packs", "bando", "pack.js")),
        "Original bando engine remains untouched");

    var terrainStaging = Path.Combine(terrainEngine, "projects", "battle-terrain");
    Directory.CreateDirectory(terrainStaging);
    ProjectFiles.SaveText(Path.Combine(terrainStaging, "config.json"),
        "{\"title\":\"Terrain Test\",\"pack\":\"terrain\",\"lang\":\"en\"}");
    var movedTerrain = projects.MoveNewProjectToWorkspace("battle-terrain", terrainEngine);
    EngineCatalog.WriteBinding(movedTerrain, importedTerrain[0]);
    var active = catalog.ResolveForProject(movedTerrain);
    Check(active.EngineId == "terrain-v1" && active.Pack == "terrain",
        "Terrain project resolves its pinned renderer");
    Check(EngineCatalog.GetProjectLanguage(movedTerrain) == "en",
        "Terrain narration language comes from project config");
    Check(catalog.ResolveForProject(copiedLegacy).Pack == "bando" ||
          EngineCatalog.GetProjectPack(copiedLegacy) == "",
        "Existing project data retains legacy compatibility");

    var terrainSkillPath = AiScriptStudio.ResolveSkillRoot(root, terrainEngine, "terrain");
    Check(new AiScriptStudio(terrainSkillPath).HasSkill,
        "Terrain AI script skill loads without bando-only dia-danh.md");

    var terrainShots = new ShotDocument(JsonNode.Parse("""
    {"duration":14,"worlds":{"lrt":{"dem":{"c":[-77.2366,39.7922]}}},
    "shots":[{"id":"s001","start":0,"end":7,"tpl":"tw",
    "args":{"world":"lrt","cam":{"az":-10,"el":44},"do":[{"show":"maine"}]}},
    {"id":"s002","start":7,"end":14,"tpl":"map2d",
    "args":{"cam":{"dist":600},"labels":[{"text":"Gettysburg"}]}}]}
    """)!);
    terrainShots.SetInitialMapDefaults("terrain");
    Check(terrainShots.Rows[0].Template == "tw" &&
          terrainShots.GetArgsJson(0).Contains("maine"),
        "Terrain templates, camera and actions are preserved");
    terrainShots.UpdateArgs(0, """{"world":"lrt","cam":{"az":20},"do":[{"show":"maine"}]}""");
    terrainShots.UpdateTemplate(1, "tw");
    terrainShots.Validate();
    Check(terrainShots.Root["worlds"]?["lrt"]?["dem"]?["c"]?[0]?.ToString() == "-77.2366" &&
          terrainShots.Root["shots"]?[1]?["tpl"]?.ToString() == "tw" &&
          !terrainShots.Root["shots"]![0]!["args"]!.AsObject().ContainsKey("view"),
        "Terrain scene editor preserves worlds and never adds bando view");

    var duplicateBlocked = false;
    try { catalog.ImportArchiveAsync(zipPath, "terrain-v1").GetAwaiter().GetResult(); }
    catch (IOException) { duplicateBlocked = true; }
    Check(duplicateBlocked, "Engine updates cannot overwrite an existing version");

    var newPack = Path.Combine(root, "future-kit");
    Directory.CreateDirectory(Path.Combine(newPack, "tools"));
    Directory.CreateDirectory(Path.Combine(newPack, "packs", "naval"));
    ProjectFiles.SaveText(Path.Combine(newPack, "render.mjs"), "// future renderer");
    ProjectFiles.SaveText(Path.Combine(newPack, "player.html"), "<html></html>");
    ProjectFiles.SaveText(Path.Combine(newPack, "packs", "naval", "pack.js"), "// naval pack");
    var futureOptions = catalog.ImportAsync(newPack, "future-v1").GetAwaiter().GetResult();
    Check(futureOptions.Any(x => x.Pack == "naval") &&
          catalog.Discover().Any(x => x.Pack == "naval"),
        "New project types are discovered without changing application code");

    var maliciousZip = Path.Combine(root, "traversal.zip");
    using (var archive = ZipFile.Open(maliciousZip, ZipArchiveMode.Create))
    {
        using var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open());
        writer.Write("unsafe");
    }
    var unsafeRejected = false;
    try { catalog.ImportArchiveAsync(maliciousZip, "malicious").GetAwaiter().GetResult(); }
    catch (InvalidDataException) { unsafeRejected = true; }
    Check(unsafeRejected && !File.Exists(Path.Combine(root, "outside.txt")),
        "ZIP path traversal blocked");

    var terrainEnvironment = new EnvironmentManager(settings, terrainEngine);
    var terrainDetected = terrainEnvironment.CheckAsync(DependencyKind.Engine).GetAwaiter().GetResult();
    Check(terrainDetected.Installed, "Environment Manager checks the selected Terrain engine");

    // Three.js is checked independently because Terrain's browser imports
    // the physical JS module; npm --depth=0 alone can miss this requirement.
    var threePkg = Path.Combine(terrainEngine, "package.json");
    ProjectFiles.SaveText(threePkg, """
      {"dependencies":{"three":"0.169.0"}}
      """);
    var threeCheck = terrainEnvironment.CheckAsync(DependencyKind.ThreeJs)
        .GetAwaiter().GetResult();
    Check(!threeCheck.Installed && threeCheck.CanInstall &&
          threeCheck.Details.Contains("three.module.js"),
          "Terrain Three.js checker detects missing runtime module");
    var moduleFile = Path.Combine(terrainEngine, "node_modules", "three", "build", "three.module.js");
    Directory.CreateDirectory(Path.GetDirectoryName(moduleFile)!);
    ProjectFiles.SaveText(moduleFile, "export const REVISION='169';");
    threeCheck = terrainEnvironment.CheckAsync(DependencyKind.ThreeJs)
        .GetAwaiter().GetResult();
    Check(threeCheck.Installed, "Terrain Three.js checker detects installed module");
    File.Delete(moduleFile);
    ProjectFiles.SaveText(threePkg, """
      {"dependencies":{"playwright":"1.56.0"}}
      """);
    threeCheck = terrainEnvironment.CheckAsync(DependencyKind.ThreeJs)
        .GetAwaiter().GetResult();
    Check(!threeCheck.Installed && !threeCheck.CanInstall &&
          threeCheck.Details.Contains("không khai báo"),
          "Terrain Three.js checker never installs an arbitrary unpinned version");
    File.Delete(threePkg);

    // Historical World is a side-by-side Multi-Pack engine with no third-party Python libs.
    var historicalFixture = Path.Combine(root, "historical-fixture");
    Directory.CreateDirectory(Path.Combine(historicalFixture, "tools"));
    Directory.CreateDirectory(Path.Combine(historicalFixture, "packs", "historical"));
    ProjectFiles.SaveText(Path.Combine(historicalFixture, "render.mjs"), "// renderer fixture");
    ProjectFiles.SaveText(Path.Combine(historicalFixture, "player.html"), "<html></html>");
    ProjectFiles.SaveText(Path.Combine(historicalFixture, "packs", "historical", "pack.js"),
        "export const packId='historical';");
    var historicalOptions = catalog.ImportAsync(historicalFixture, "historical-mvp")
        .GetAwaiter().GetResult();
    Check(historicalOptions.Length == 1 && historicalOptions[0].Pack == "historical",
        "Multi-Pack discovers new Historical World 3D engine without editing C# pack catalogs");
    var historicalEnvironment = new EnvironmentManager(settings, historicalOptions[0].EngineRoot);
    var historicalPython = historicalEnvironment.CheckAsync(DependencyKind.PythonPackages)
        .GetAwaiter().GetResult();
    Check(historicalPython.Installed && !historicalPython.CanInstall,
        "Historical World standard-library Python helpers need no numpy/scipy bootstrap");
    var historicalProject = Path.Combine(workspace, "historical-test");
    Directory.CreateDirectory(historicalProject);
    ProjectFiles.SaveText(Path.Combine(historicalProject, "config.json"),
        """{"title":"Historical fixture","pack":"historical"}""");
    EngineCatalog.WriteBinding(historicalProject, historicalOptions[0]);
    Check(catalog.ResolveForProject(historicalProject).EngineId == "historical-mvp",
        "Historical project stays bound to its independent renderer");

    var unpackedExample = Path.Combine(root, "unpacked-sample", "terrain-mau");
    Directory.CreateDirectory(Path.Combine(unpackedExample, "story"));
    ProjectFiles.SaveText(Path.Combine(unpackedExample, "config.json"),
        "{\"title\":\"Sample Terrain\",\"pack\":\"terrain\",\"lang\":\"en\"}");
    ProjectFiles.SaveText(Path.Combine(unpackedExample, "story", "shots.json"),
        "{\"worlds\":{},\"shots\":[]}");
    var importedExample = projects.ImportExternalProjectToWorkspace(unpackedExample);
    Check(File.Exists(Path.Combine(importedExample, "story", "shots.json")) &&
          File.Exists(Path.Combine(unpackedExample, "story", "shots.json")),
        "Existing Terrain sample imported without changing its original");
    Check(catalog.ResolveForProject(importedExample).Pack == "terrain",
        "Imported Terrain example finds the installed matching pack");
    var exampleCollision = false;
    try { projects.ImportExternalProjectToWorkspace(unpackedExample); }
    catch (IOException) { exampleCollision = true; }
    Check(exampleCollision, "Import cannot overwrite an existing workspace project");

    // Grounded Terrain AI Scene Builder: no real API calls or project writes
    // until user explicitly confirms the generated candidate.
    var aiTerrainProject = Path.Combine(root, "terrain-ai-test");
    var terrainSlug = Path.GetFileName(aiTerrainProject);
    Directory.CreateDirectory(Path.Combine(aiTerrainProject, "story"));
    ProjectFiles.SaveText(Path.Combine(aiTerrainProject, "config.json"),
        "{\"pack\":\"terrain\",\"lang\":\"en\"}");
    ProjectFiles.SaveText(Path.Combine(aiTerrainProject, "03-HINH-" + terrainSlug + ".md"),
        "A schematic training battlefield; coordinates are unknown, never claim real DEM.");
    var terrainDraft = """
    {"duration":11,"shots":[
      {"id":"s001","start":0,"end":4.33,"line":"Introduce the battlefield.","original_marker":9},
      {"id":"s002","start":4.33,"end":11,"line":"The camera follows the hill."}
    ]}
    """;
    var draftPath = Path.Combine(aiTerrainProject, "story", "shots.json");
    ProjectFiles.SaveText(draftPath, terrainDraft);
    var testTerrainEngine = Path.Combine(root, "terrain-ai-engine");
    var referenceRoot = Path.Combine(testTerrainEngine, "skills", "lam-video-terrain", "references");
    Directory.CreateDirectory(referenceRoot);
    ProjectFiles.SaveText(Path.Combine(referenceRoot, "mau-canh.md"),
        "Use tw camera c/dist/az/el, worlds size and units.");
    ProjectFiles.SaveText(Path.Combine(referenceRoot, "phong-cach.md"),
        "Terrain is schematic; avoid imaginary GPS coordinates.");
    var fakeCalls = 0;
    Task<string> CompleteTerrain(string prompt, CancellationToken ct)
    {
        fakeCalls++;
        return Task.FromResult(prompt.Contains("NHIỆM VỤ WORLDS", StringComparison.Ordinal)
            ? """
              {"worlds":{"hills":{"size":[2000,1500],
              "hills":[{"p":[20,20],"r":100,"h":40}],
              "rivers":[{"pts":[[-400,-100],[50,0],[450,100]],"w":15}],
              "woods":[{"pts":[[-250,100],[-100,100],[-100,300],[-250,300]],"step":20}],
              "units":[{"id":"blue","side":"blue","kind":"pawn","p":[0,0]}]}}}
              """
            : """
              {"shots":[
               {"id":"s001","tpl":"tw",
                "args":{"world":"hills",
                "cam":{"c":[0,0],"dist":900,"az":0,"el":60},
                "tags":[{"t":"Terrain schematic"}]}},
               {"id":"s002","tpl":"tw",
                "args":{"world":"hills",
                "cam":{"c":[40,0],"dist":450,"az":30,"el":35},
                "do":[{"show":"blue","at":0.5}],
                "tags":[{"t":"Hill movement"}]}}]}
              """);
    }
    var terrainAssistant = new AiScriptStudio(skillFixture);
    var terrainBuilder = new AiTerrainSceneBuilder(terrainAssistant, CompleteTerrain);
    var terrainRequest = new AiScriptRequest(AiScriptProvider.Custom, "fake-model", "",
        "Terrain fixture", 12, "http://127.0.0.1:8080/v1");
    var candidateTerrain = terrainBuilder.GenerateAsync(terrainRequest,
        aiTerrainProject, testTerrainEngine).GetAwaiter().GetResult();
    Check(fakeCalls == 2, "AI Terrain uses worlds + batched shots completion");
    Check(candidateTerrain.Root["worlds"]?["hills"]?["dem"] is null,
        "No fabricated DEM coordinates when the plan has none");
    Check(candidateTerrain.Rows.Count == 2 && candidateTerrain.Rows[0].Template == "tw" &&
          candidateTerrain.Rows[1].Template == "tw",
        "AI Terrain assigns valid 3D templates to all scenes");
    Check(candidateTerrain.Rows[0].Start == 0 &&
          candidateTerrain.Rows[0].End == 4.33 &&
          candidateTerrain.Rows[1].Line == "The camera follows the hill." &&
          candidateTerrain.Root["shots"]?[0]?["original_marker"]?.ToString() == "9",
        "AI Terrain preserves timeline, voice and unknown scene metadata");
    Check(File.ReadAllText(draftPath) == terrainDraft &&
          !Directory.GetFiles(Path.Combine(aiTerrainProject, "story"), "*.backup-*").Any(),
        "AI Terrain generation does not overwrite scenes without approval");
    AiTerrainSceneBuilder.SaveWithBackup(aiTerrainProject, candidateTerrain);
    Check(File.ReadAllText(draftPath).Contains("\"worlds\"") &&
          Directory.GetFiles(Path.Combine(aiTerrainProject, "story"), "shots.json.backup-*").Length == 1,
        "AI Terrain writes generated scenes with timestamped backup");
    // Static scene-quality audit catches empty maps, malformed geometry and on-screen debug notes.
    var grayTerrain = JsonNode.Parse("""
      {"worlds":{"W01":{"size":[1000,800],"ground":"#ebe4cf","haze":"#ede7d8",
        "hills":[{"p":[0,0],"r":200,"h":30}],"undulate":0,"contours":0}},
       "shots":[{"id":"s001","start":0,"end":2,"line":"River near the capital",
         "tpl":"tw","args":{"world":"W01",
           "cam":{"c":[0,0],"dist":900,"az":0,"el":60},"note":"Terrain schematic"}}]}
      """)!;
    var grayIssues = TerrainSceneQuality.Evaluate(grayTerrain);
    Check(TerrainSceneQuality.HasBlockingIssues(grayIssues) &&
          grayIssues.Any(i => i.Code == "SCHEMATIC_ONLY") &&
          grayIssues.Any(i => i.Code == "LOW_CONTRAST") &&
          grayIssues.Any(i => i.Code == "VISIBLE_NOTE"),
          "AI Terrain quality detects schematic-only gray map and visible internal note");

    var brokenGeometry = JsonNode.Parse("""
      {"worlds":{"W01":{"size":[1000,800],"rivers":[{"pts":[[0,0]]}]}},
       "shots":[]}
      """)!;
    Check(TerrainSceneQuality.Evaluate(brokenGeometry)
        .Any(i => i.Code == "POINTS_INVALID" && i.Blocking),
        "AI Terrain quality rejects malformed river geometry");
    Check(!TerrainSceneQuality.HasBlockingIssues(
          TerrainSceneQuality.Evaluate(candidateTerrain.Root)),
          "AI Terrain passes quality audit for richly layered world");
    Check(!File.ReadAllBytes(draftPath).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
        "AI Terrain writes shots.json in UTF-8 without BOM");

    // V3 geometry/composition regression: sample must be diagnosed without changing shots.
    var v3FlatScene = JsonNode.Parse("""
      {"worlds":{"W01":{"size":[1000,800],
        "hills":[{"p":[0,0],"r":180,"h":60}],
        "fields":[{"pts":[[-450,-350],[450,-350],[450,350],[-450,350]],"color":"#aaff66"}],
        "rivers":[{"pts":[[-400,0],[0,0],[400,300]],"w":20}],
        "villages":[{"p":[0,0],"n":8}]}},
       "shots":[{"id":"s001","start":0,"end":2,"line":"Kinh đô bên sông",
         "tpl":"tw","args":{"world":"W01",
           "cam":{"c":[0,0],"dist":2100,"az":0,"el":80}}}]}
      """)!;
    var v3Issues = TerrainSceneQuality.Evaluate(v3FlatScene);
    Check(v3Issues.Any(i => i.Code == "OVERSIZED_FIELD") &&
          v3Issues.Any(i => i.Code == "ANGULAR_RIVER") &&
          v3Issues.Any(i => i.Code == "DISTANT_CAMERA") &&
          v3Issues.Any(i => i.Code == "TOPDOWN_SETTLEMENT"),
          "Terrain v3 detects oversized polygon, angular river and distant top-down camera");

    var v3InvalidPolyline = JsonNode.Parse("""
      {"worlds":{"W01":{"size":[1000,800],
        "hills":[{"p":[0,0],"r":180,"h":40}],
        "rivers":[{"pts":[[0,0],[0,0],[15,0],[20,0]]}]}},
       "shots":[]}
      """)!;
    Check(TerrainSceneQuality.Evaluate(v3InvalidPolyline)
        .Any(i => i.Code == "ANGULAR_RIVER") == false,
        "Terrain v3 accepts four-point polyline while flagging simple ones");

    // V4: subject-aware framing and historical finale warning must reference existing units.
    var v4WideFinale = JsonNode.Parse("""
      {"worlds":{"W01":{"size":[2000,1500],"hills":[{"p":[0,0],"r":200,"h":50}],
        "rivers":[{"pts":[[-600,-100],[-400,0],[0,100],[500,150]]}],
        "villages":[{"p":[0,0],"n":12}],
        "units":[{"id":"capital","p":[0,0],"kind":"flag","side":"pale"}]}},
       "shots":[
        {"id":"s001","start":0,"end":3,"line":"Giới thiệu địa hình",
         "tpl":"tw","args":{"world":"W01","cam":{"c":[0,0],"dist":1800,"az":10,"el":50}}},
        {"id":"s002","start":3,"end":6,"line":"Thăng Long trở thành kinh đô",
         "tpl":"tw","args":{"world":"W01","pins":[{"u":"capital","t":"Thăng Long"}],
           "cam":{"c":[900,650],"dist":2900,"az":0,"el":78}}}]}
      """)!;
    var v4Problems = TerrainSceneQuality.Evaluate(v4WideFinale);
    Check(v4Problems.Any(i => i.Code == "SUBJECT_OFF_CENTER" && i.Subject == "s002") &&
          v4Problems.Any(i => i.Code == "SUBJECT_TOO_SMALL" && i.Subject == "s002") &&
          v4Problems.Any(i => i.Code == "WEAK_FINALE" && i.Subject == "s002"),
          "Terrain v4 detects an off-center, distant weak capital finale");
    var correctedCamera = (JsonObject)v4WideFinale["shots"]![1]!["args"]!["cam"]!;
    correctedCamera["c"] = new JsonArray(0, 0);
    correctedCamera["dist"] = 700;
    correctedCamera["el"] = 42;
    var correctedProblems = TerrainSceneQuality.Evaluate(v4WideFinale);
    Check(!correctedProblems.Any(i => i.Subject == "s002" &&
          TerrainSceneQuality.CameraRepairCodes.Contains(i.Code)),
          "Terrain v4 passes a focused finale camera without changing world or voice");

    // Integration: v4 requests focused AI repair and accepts only camera changes.
    var v4Calls = 0;
    Task<string> RepairTerrain(string prompt, CancellationToken ct)
    {
        v4Calls++;
        if (prompt.Contains("NHIỆM VỤ WORLDS", StringComparison.Ordinal))
            return Task.FromResult("""
                {"worlds":{"hills":{"size":[2000,1500],
                  "hills":[{"p":[0,0],"r":150,"h":70}],
                  "rivers":[{"pts":[[-700,-100],[-250,0],[250,150],[650,200]]}],
                  "villages":[{"p":[0,0],"n":10}],
                  "units":[{"id":"capital","p":[0,0],"kind":"flag","side":"pale"}]}}}
                """);
        if (prompt.Contains("NHIỆM VỤ SỬA CAMERA V4", StringComparison.Ordinal))
            return Task.FromResult("""
                {"shots":[{"id":"s002","args":{
                  "world":"hills","pins":[{"u":"capital","t":"Capital"}],
                  "cam":{"c":[0,0],"dist":700,"az":10,"el":40}}}]}
                """);
        return Task.FromResult("""
            {"shots":[
              {"id":"s001","tpl":"tw","args":{"world":"hills",
                "cam":{"c":[0,0],"dist":900,"az":5,"el":50}}},
              {"id":"s002","tpl":"tw","args":{"world":"hills",
                "pins":[{"u":"capital","t":"Capital"}],
                "cam":{"c":[900,600],"dist":3500,"az":10,"el":78}}}]}
            """);
    }
    // The second narration in the fixture is not a capital scene; pinned-unit
    // size and off-center checks still trigger the repair independently.
    var repairedTerrain = new AiTerrainSceneBuilder(terrainAssistant, RepairTerrain)
        .GenerateAsync(terrainRequest, aiTerrainProject, testTerrainEngine)
        .GetAwaiter().GetResult();
    Check(v4Calls == 3 &&
          repairedTerrain.Root["shots"]?[1]?["args"]?["cam"]?["dist"]?.ToString() == "700" &&
          repairedTerrain.Root["shots"]?[1]?["line"]?.ToString() == "The camera follows the hill." &&
          repairedTerrain.Root["shots"]?[1]?["args"]?["pins"]?[0]?["u"]?.ToString() == "capital",
          "Terrain v4 AI repair preserves narration and pins while correcting scene camera");

    // V5 cinematic rendering is enabled only when the imported engine supports it.
    var cinematicWorldJs = Path.Combine(testTerrainEngine, "packs", "terrain", "world.js");
    Directory.CreateDirectory(Path.GetDirectoryName(cinematicWorldJs)!);
    ProjectFiles.SaveText(cinematicWorldJs,
        "// V5 optical ruggedness: renderer capability signature");
    var v5Candidate = new AiTerrainSceneBuilder(terrainAssistant, CompleteTerrain)
        .GenerateAsync(terrainRequest, aiTerrainProject, testTerrainEngine)
        .GetAwaiter().GetResult();
    Check(v5Candidate.Root["worlds"]?["hills"]?["graphics"]?.ToString() == "cinematic" &&
          v5Candidate.Root["worlds"]?["hills"]?["terrainDetail"]?.ToString() == "0.85" &&
          v5Candidate.Root["worlds"]?["hills"]?["exposure"]?.ToString() == "1.22",
          "Terrain v5 enables cinematic graphics only with patched imported engine");
    File.Delete(cinematicWorldJs);
    var legacyCandidate = new AiTerrainSceneBuilder(terrainAssistant, CompleteTerrain)
        .GenerateAsync(terrainRequest, aiTerrainProject, testTerrainEngine)
        .GetAwaiter().GetResult();
    Check(legacyCandidate.Root["worlds"]?["hills"]?["graphics"] is null,
        "Terrain v5 keeps legacy engine worlds unmodified");

    var concurrentEditBlocked = false;
    try { AiTerrainSceneBuilder.SaveWithBackup(aiTerrainProject, candidateTerrain, terrainDraft); }
    catch (IOException) { concurrentEditBlocked = true; }
    Check(concurrentEditBlocked &&
          Directory.GetFiles(Path.Combine(aiTerrainProject, "story"), "shots.json.backup-*").Length == 1,
        "AI Terrain rejects stale save without a second backup");


    Task<string> InvalidScene(string prompt, CancellationToken ct) =>
        Task.FromResult(prompt.Contains("NHIỆM VỤ WORLDS")
            ? "{\"worlds\":{\"hills\":{\"size\":[1000,800]}}}"
            : "{\"shots\":[{\"id\":\"s001\",\"tpl\":\"map\",\"args\":{}},{\"id\":\"s002\",\"tpl\":\"tw\",\"args\":{}}]}");
    var invalidRejected = false;
    try
    {
        new AiTerrainSceneBuilder(terrainAssistant, InvalidScene)
            .GenerateAsync(terrainRequest, aiTerrainProject, testTerrainEngine).GetAwaiter().GetResult();
    }
    catch (InvalidDataException) { invalidRejected = true; }
    Check(invalidRejected, "AI Terrain rejects unsupported templates without modifying project");

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
