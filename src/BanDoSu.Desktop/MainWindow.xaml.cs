using BanDoSu.Core;
using Microsoft.Win32;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BanDoSu.Desktop;

public partial class MainWindow : Window
{
    private StudioSettings _settings = StudioSettings.Load();
    private EngineCatalog _engineCatalog;
    private ProjectFiles _projects;
    private StudioCommands _commands;
    private ProjectSummary? _selected;
    private ShotDocument? _shots;
    private string? _scriptPath;
    private CancellationTokenSource? _operation;
    private Process? _previewHost;
    private string? _previewUrl;
    private readonly ObservableCollection<EnvironmentRow> _environmentRows = new();
    private CancellationTokenSource? _environmentCts;
    private CancellationTokenSource? _aiCts;
    private CancellationTokenSource? _terrainAiCts;
    private CancellationTokenSource? _ttsCts;
    private ITtsSynthesisProvider? _ttsProvider = null; // Replaced by an adapter when TTS API is supplied.
    private sealed record EnvironmentRow(DependencyKind Kind, string Name, string Status, string Details);


    public MainWindow()
    {
        InitializeComponent();
        EnvironmentGrid.ItemsSource = _environmentRows;
        MapBrowser.NavigationCompleted += async (_, _) =>
        {
            try
            {
                if (MapBrowser.CoreWebView2 == null) return;
                await MapBrowser.ExecuteScriptAsync(
                    "(async () => { for (let i = 0; i < 150 && !window.__ready && !window.__bootError; i++) await new Promise(r => setTimeout(r, 100)); if (window.__ready) await window.renderFrame(0); })()");
                var bootError = await MapBrowser.ExecuteScriptAsync("window.__bootError || null");
                if (bootError != "null") Log("Lỗi khởi tạo bản đồ: " + bootError);
            }
            catch (Exception ex) { Log("Lỗi xem trước bản đồ: " + ex.Message); }
        };
        _projects = new ProjectFiles(_settings);
        _commands = new StudioCommands(_settings);
        _engineCatalog = new EngineCatalog(_settings);
        FillSettings();
        RefreshPacks();
        RefreshTtsVoiceChoices();
        AiCustomBaseUrl.Text = _settings.CustomAiBaseUrl;
        AiCustomEndpointInput.SelectedIndex = _settings.CustomAiEndpoint == AiCustomEndpoint.Responses ? 1 : 0;
        RefreshProjects();
    }

    private void FillSettings()
    {
        StudioRootText.Text = _settings.StudioRoot;
        WorkspaceRootText.Text = _settings.WorkspaceRoot;
        ExportRootText.Text = _settings.ExportRoot;
        NodeText.Text = _settings.NodeExecutable;
        PythonText.Text = _settings.PythonExecutable;
        FfmpegText.Text = _settings.FfmpegExecutable;
        RefreshPortableStatus();
    }

    private void Log(string text) => Dispatcher.InvokeAsync(() =>
    {
        LogOutput.AppendText(text + Environment.NewLine);
        LogOutput.ScrollToEnd();
    });

    private void Alert(string message) => MessageBox.Show(this, message, "Bản Đồ Sử Studio",
        MessageBoxButton.OK, MessageBoxImage.Information);

    private bool HasStudio()
    {
        if (_engineCatalog.Discover().Count > 0) return true;
        Alert("Chưa có engine H2 hỗ trợ Pack. Hãy nhập ZIP H2/Terrain ở tab Dự án.");
        MainTabs.SelectedIndex = 6; // Cài đặt
        return false;
    }

    private bool HasProject()
    {
        if (!HasStudio()) return false;
        if (_selected is not null) return true;
        Alert("Vui lòng chọn dự án trong tab Dự án trước.");
        MainTabs.SelectedIndex = 0;
        return false;
    }

    private async Task ExecuteAsync(Func<Action<string>, CancellationToken, Task<ProcessResult>> work, string label,
        Action? onSuccess = null)
    {
        if (_operation != null || _environmentCts != null) { Alert("Đang có tác vụ khác chạy. Hãy hoàn tất hoặc hủy tác vụ đó trước."); return; }
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation;
        RenderStatus.Text = label + " đang chạy...";
        Log("=== " + label + " ===");
        try
        {
            var result = await work(Log, cancellation.Token);
            if (result.ExitCode != 0)
            {
                RenderStatus.Text = label + " thất bại";
                Log($"LỖI: mã thoát {result.ExitCode}");
                Alert(label + " thất bại. Hãy xem nhật ký xử lý.");
                return;
            }
            RenderStatus.Text = label + " hoàn tất";
            Log("HOÀN TẤT: " + label);
            onSuccess?.Invoke();
        }
        catch (OperationCanceledException)
        {
            RenderStatus.Text = "Đã hủy";
            Log("ĐÃ HỦY: " + label);
        }
        catch (Exception ex)
        {
            RenderStatus.Text = label + " thất bại";
            Log(ex.ToString());
            Alert(ex.Message);
        }
        finally { _operation = null; }
    }

    private void RefreshProjects()
    {
        ProjectsList.ItemsSource = null;
        if (!Directory.Exists(_projects.ProjectsRoot)) return;
        ProjectsList.ItemsSource = _projects.List();
        if (_selected != null)
        {
            ProjectsList.SelectedItem = _projects.List().FirstOrDefault(p => p.Name == _selected.Name);
        }
    }


    private void RefreshPacks()
    {
        if (NewPackCombo is null) return;
        var previous = NewPackCombo.SelectedItem as PackOption;
        var profiles = _engineCatalog.Discover();
        NewPackCombo.ItemsSource = profiles;
        NewPackCombo.SelectedItem = profiles.FirstOrDefault(x =>
            x.Pack == previous?.Pack && x.EngineId == previous?.EngineId) ??
            profiles.FirstOrDefault(x => x.Pack == "bando") ?? profiles.FirstOrDefault();
        EnvironmentEngineCombo.ItemsSource = profiles;
        EnvironmentEngineCombo.SelectedItem = profiles.FirstOrDefault(x => x.EngineId == "default") ??
            profiles.FirstOrDefault();
        PackStatus.Text = profiles.Count == 0
            ? "Chưa có pack. Nhập ZIP có kit/ hoặc chọn thư mục engine."
            : "Đã phát hiện " + profiles.Count + " lựa chọn: " +
              string.Join(", ", profiles.Select(x => x.DisplayName));
    }

    private void RefreshPacksClick(object sender, RoutedEventArgs e) => RefreshPacks();

    private void NewPackChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NewPackCombo.SelectedItem is PackOption option)
        {
            NewLangInput.Text = option.Language;
            NewStyleInput.Text = option.Style;
        }
    }

    private async void ImportPackZipClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "H2 Studio ZIP (*.zip)|*.zip",
            Title = "Chọn ZIP H2 Studio (h2-terrain-studio.zip hoặc pack mới)" };
        if (dialog.ShowDialog(this) != true) return;
        await ImportPackAsync(dialog.FileName, isZip: true);
    }

    private async void ImportPackFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog {
            Title = "Chọn thư mục kit/ có render.mjs, player.html, tools/ và packs/" };
        if (dialog.ShowDialog(this) != true) return;
        await ImportPackAsync(dialog.FolderName, isZip: false);
    }

    private async Task ImportPackAsync(string source, bool isZip)
    {
        if (_operation != null || _environmentCts != null || _aiCts != null || _ttsCts != null)
        {
            Alert("Đang có tác vụ khác chạy."); return;
        }
        var proposed = Path.GetFileNameWithoutExtension(source)
            .ToLowerInvariant().Replace(' ', '-');
        proposed = Regex.Replace(proposed, @"[^a-z0-9_-]", "-");
        if (!Regex.IsMatch(proposed, @"^[a-z0-9][a-z0-9_-]{0,80}$"))
            proposed = "engine-" + DateTime.UtcNow.ToString("yyyyMMdd");
        // Versioned installation: never replace an existing renderer or any project files.
        var name = proposed + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        if (MessageBox.Show(this,
                "Nhập engine '" + name + "' từ:\n" + source +
                "\n\nEngine mới sẽ được lưu song song tại studio/engines/" + name +
                ". Không ghi đè H2 cũ. Các thư viện npm/Python của mỗi engine cần cài riêng.",
                "Xác nhận nhập H2 Engine", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        using var cts = new CancellationTokenSource();
        _operation = cts;
        try
        {
            PackStatus.Text = "Đang nhập engine '" + name + "'...";
            var packs = isZip
                ? await _engineCatalog.ImportArchiveAsync(source, name, cts.Token)
                : await _engineCatalog.ImportAsync(source, name, cts.Token);
            RefreshPacks();
            NewPackCombo.SelectedItem = packs[0];
            PackStatus.Text = "Đã cài " + string.Join(", ", packs.Select(p => p.Pack)) +
                ". Mở Quản lý môi trường, chọn engine mới và cài dependency.";
            Log(PackStatus.Text);
        }
        catch (OperationCanceledException) { PackStatus.Text = "Đã hủy nhập engine."; }
        catch (Exception ex)
        {
            PackStatus.Text = "Lỗi nhập engine: " + ex.Message;
            Log(PackStatus.Text); Alert(PackStatus.Text);
        }
        finally { _operation = null; }
    }

    private void UpdateSelectedEngineInfo()
    {
        if (_selected is null) return;
        try
        {
            var current = _engineCatalog.ResolveForProject(_selected.Directory);
            SelectedEngineInfo.Text = "Dự án " + _selected.Name + " / Pack " +
                current.Pack + " / Engine " + current.EngineId;
            var match = _engineCatalog.Discover().FirstOrDefault(p =>
                p.Pack == current.Pack && p.EngineId == current.EngineId);
            if (match != null) NewPackCombo.SelectedItem = match;
        }
        catch (Exception ex)
        {
            SelectedEngineInfo.Text = "Engine của dự án chưa sẵn sàng: " + ex.Message;
        }
    }

    private void BindProjectEngineClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || NewPackCombo.SelectedItem is not PackOption profile)
        {
            Alert("Hãy chọn dự án và Pack/Engine ở danh sách."); return;
        }
        var projectPack = EngineCatalog.GetProjectPack(_selected.Directory);
        if (!projectPack.Equals(profile.Pack, StringComparison.OrdinalIgnoreCase))
        {
            Alert("Không thể gắn engine khác loại. Dự án dùng pack '" +
                projectPack + "', nhưng engine được chọn là '" + profile.Pack + "'."); return;
        }
        if (MessageBox.Show(this, "Đổi engine cho dự án sang '" + profile.EngineId +
                "'? Hãy kiểm tra lại tính tương thích các cảnh với phiên bản renderer mới.",
                "Gắn phiên bản Engine", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        EngineCatalog.WriteBinding(_selected.Directory, profile);
        UpdateSelectedEngineInfo();
        StopPreviewHost();
        Log("Đã gắn dự án với engine " + profile.EngineId);
    }

    private void EnvSelectProjectEngineClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) { Alert("Hãy chọn dự án."); return; }
        try
        {
            var profile = _engineCatalog.ResolveForProject(_selected.Directory);
            EnvironmentEngineCombo.SelectedItem = _engineCatalog.Discover()
                .FirstOrDefault(p => p.EngineId == profile.EngineId && p.Pack == profile.Pack);
            MainTabs.SelectedIndex = 7;
        }
        catch (Exception ex) { Alert(ex.Message); }
    }

    private string EnvironmentEngineRoot =>
        (EnvironmentEngineCombo.SelectedItem as PackOption)?.EngineRoot ?? _settings.StudioRoot;

    private void RefreshProjectsClick(object sender, RoutedEventArgs e) => RefreshProjects();

    private async void CreateProjectClick(object sender, RoutedEventArgs e)
    {
        if (!HasStudio()) return;
        var slug = NewSlug.Text.Trim();
        if (!Regex.IsMatch(slug, @"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,80}$"))
        { Alert("Tên thư mục dự án chỉ được chứa chữ Latin không dấu, chữ số, dấu gạch nối và gạch dưới."); return; }
        if (_projects.Exists(slug))
        { Alert("Dự án đã tồn tại. Hãy chọn tên khác."); return; }
        if (NewPackCombo.SelectedItem is not PackOption basePack)
        {
            Alert("Chọn Pack + phiên bản engine trước khi tạo dự án."); return;
        }
        if (!Regex.IsMatch(NewLangInput.Text.Trim(), @"^[a-zA-Z]{2,5}(-[a-zA-Z]{2,5})?$"))
        {
            Alert("Mã ngôn ngữ không hợp lệ."); return;
        }
        var style = NewStyleInput.Text.Trim();
        if (!Regex.IsMatch(style, @"^[a-zA-Z0-9_-]{1,32}$"))
        {
            Alert("Style phải là chữ, số, _ hoặc -."); return;
        }
        var profile = basePack with { Language = NewLangInput.Text.Trim(), Style = style };
        await ExecuteAsync((log, ct) => _commands.CreateProjectAsync(slug, NewTitle.Text.Trim(),
            NewCaptions.IsChecked == true, profile, log, ct), "Tạo dự án", () =>
        {
            RefreshProjects();
            ProjectsList.SelectedItem = _projects.List().FirstOrDefault(p => p.Name == slug);
        });
    }

    private void ProjectsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = ProjectsList.SelectedItem as ProjectSummary;
        _shots = null;
        ShotsGrid.ItemsSource = null;
        ArgsEditor.Text = "";
        ProjectHeader.Text = _selected?.Title ?? "Chưa chọn dự án";
        if (_selected == null)
        {
            SelectedEngineInfo.Text = "Engine dự án: chưa chọn";
            return;
        }
        UpdateSelectedEngineInfo();
        var projectScript = _projects.FileInProject(_selected.Name, "input", "02-VOICE.txt");
        _scriptPath = projectScript;
        ScriptEditor.Text = File.Exists(projectScript) ? File.ReadAllText(projectScript, Encoding.UTF8) : "";
        VoiceFolder.Text = _projects.FileInProject(_selected.Name, "input", "voice");
        TtsStatusText.Text = "Sẵn sàng chọn giọng đọc; API TTS chưa được cấu hình.";
        LoadAiFiles();
        try { LoadShots(); } catch (Exception ex) { Log("Lỗi tải cảnh: " + ex.Message); }
        StopPreviewHost();
    }

    private void ImportExistingProjectClick(object sender, RoutedEventArgs e)
    {
        if (_operation != null || _environmentCts != null || _aiCts != null || _ttsCts != null)
        {
            Alert("Đang có tác vụ khác chạy."); return;
        }
        var dialog = new OpenFolderDialog {
            Title = "Chọn dự án H2 gốc (chứa config.json, ví dụ terrain-mau)" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var pack = EngineCatalog.GetProjectPack(dialog.FolderName);
            if (!_engineCatalog.Discover().Any(x => x.Pack.Equals(pack, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Chưa cài engine hỗ trợ pack '" + pack +
                    "'. Hãy nhập ZIP/engine tương ứng trước.");
            var destination = _projects.ImportExternalProjectToWorkspace(dialog.FolderName);
            Log("Đã nhập dự án " + pack + " vào " + destination);
            RefreshProjects();
            ProjectsList.SelectedItem = _projects.List()
                .FirstOrDefault(x => x.Name == Path.GetFileName(destination));
            Alert("Đã nhập dự án '" + Path.GetFileName(destination) +
                "'. Nên dùng nút Gắn dự án với engine đã chọn để khóa phiên bản renderer.");
        }
        catch (Exception ex) { Alert("Không thể nhập dự án: " + ex.Message); }
    }

    private void OpenProjectFolderClick(object sender, RoutedEventArgs e)
    { if (HasProject()) OpenPath(_selected!.Directory); }

    private async void MigrateLegacyClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        var selected = _selected!;
        if (!selected.IsLegacy)
        {
            Alert("Dự án này đã nằm trong Workspace.");
            return;
        }
        var confirmation = MessageBox.Show(this,
            "Sao chép toàn bộ dự án này vào Workspace? Bản gốc trong engine sẽ được giữ lại, không xóa.",
            "Xác nhận sao chép dự án", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes) return;
        try
        {
            var folder = await Task.Run(() => _projects.CopyLegacyProjectToWorkspace(selected.Name));
            Log("Đã sao chép dự án cũ sang: " + folder);
            RefreshProjects();
            ProjectsList.SelectedItem = _projects.List().FirstOrDefault(p => p.Name == selected.Name);
        }
        catch (Exception ex) { Alert("Không thể sao chép dự án: " + ex.Message); }
    }


    private void LoadScriptClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Tệp văn bản (*.txt)|*.txt|Tất cả tệp|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        _scriptPath = dlg.FileName;
        ScriptEditor.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
        Log("Đã tải kịch bản: " + _scriptPath);
    }

    private bool SaveScript()
    {
        if (!HasProject()) return false;
        // Always make the project's copy canonical, even if the source was external.
        var canonical = _projects.FileInProject(_selected!.Name, "input", "02-VOICE.txt");
        ProjectFiles.SaveText(canonical, ScriptEditor.Text);
        _scriptPath = canonical;
        Log("Đã lưu lời đọc: " + canonical);
        return true;
    }

    private void RefreshTtsVoiceChoices()
    {
        var selectedId = _settings.TtsSelectedVoiceId ?? "";
        TtsVoiceCombo.ItemsSource = null;
        TtsVoiceCombo.ItemsSource = (_settings.TtsVoiceIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        TtsVoiceCombo.Text = selectedId;
    }

    private void TtsSaveVoiceClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = TtsBatch.RequireVoiceId(TtsVoiceCombo.Text);
            _settings.TtsVoiceIds ??= [];
            if (!_settings.TtsVoiceIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                _settings.TtsVoiceIds.Add(id);
            _settings.TtsSelectedVoiceId = id;
            _settings.Save();
            RefreshTtsVoiceChoices();
            TtsStatusText.Text = "Đã lưu Voice ID: " + id +
                ". Sau khi tích hợp API TTS, giọng này sẽ được dùng để tạo MP3.";
        }
        catch (Exception ex) { Alert(ex.Message); }
    }

    private void TtsRemoveVoiceClick(object sender, RoutedEventArgs e)
    {
        var id = TtsVoiceCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(id)) return;
        _settings.TtsVoiceIds ??= [];
        _settings.TtsVoiceIds.RemoveAll(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(_settings.TtsSelectedVoiceId, id, StringComparison.OrdinalIgnoreCase))
            _settings.TtsSelectedVoiceId = _settings.TtsVoiceIds.FirstOrDefault() ?? "";
        _settings.Save();
        RefreshTtsVoiceChoices();
        TtsStatusText.Text = "Đã xóa Voice ID khỏi danh sách, không xóa file MP3 đã tạo.";
    }

    private async void TtsGenerateClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            Alert("Hãy chọn dự án trước khi tạo MP3.");
            MainTabs.SelectedIndex = 0;
            return;
        }
        if (_operation != null || _environmentCts != null || _aiCts != null || _ttsCts != null)
        {
            Alert("Đang có tác vụ khác. Hãy hoàn thành hoặc hủy trước khi tạo MP3.");
            return;
        }

        IReadOnlyList<TtsSegment> segments;
        string voiceId;
        try
        {
            segments = TtsBatch.ParseSegments(ScriptEditor.Text);
            voiceId = TtsBatch.RequireVoiceId(TtsVoiceCombo.Text);
            if (string.IsNullOrWhiteSpace(VoiceFolder.Text))
                throw new InvalidDataException("Hãy chọn thư mục xuất MP3.");
        }
        catch (Exception ex) { Alert(ex.Message); return; }

        // Avoid implying synthesis succeeded while no concrete backend exists.
        if (_ttsProvider is null)
        {
            TtsStatusText.Text = $"Đã chuẩn bị {segments.Count} đoạn, giọng '{voiceId}', " +
                $"đầu ra {VoiceFolder.Text}. Chưa cấu hình API TTS nên chưa có MP3 nào được tạo.";
            Log("TTS: " + TtsStatusText.Text);
            Alert("Chưa cấu hình API TTS. Khi bạn cung cấp endpoint, payload, xác thực và định dạng trả về, " +
                "tính năng Tạo MP3 sẽ được nối vào bộ xử lý sẵn có.");
            return;
        }

        using var cts = new CancellationTokenSource();
        _ttsCts = cts;
        TtsGenerateButton.IsEnabled = false;
        TtsCancelButton.IsEnabled = true;
        try
        {
            var progress = new Progress<TtsProgress>(p =>
            {
                TtsStatusText.Text = $"TTS: {p.Completed}/{p.Total} — {p.Message}";
                Log(TtsStatusText.Text);
            });
            var total = await TtsBatch.GenerateAsync(
                ScriptEditor.Text, voiceId, VoiceFolder.Text, _ttsProvider, progress, cts.Token);
            TtsStatusText.Text = $"Đã tạo {total} MP3 trong {VoiceFolder.Text}.";
            Log("TTS: " + TtsStatusText.Text);
        }
        catch (OperationCanceledException)
        {
            TtsStatusText.Text = "Đã hủy TTS. Những MP3 hoàn thành trước đó được giữ lại.";
        }
        catch (Exception ex)
        {
            TtsStatusText.Text = "Tạo MP3 thất bại: " + ex.Message;
            Log("TTS: " + TtsStatusText.Text);
            Alert(TtsStatusText.Text);
        }
        finally
        {
            _ttsCts = null;
            TtsGenerateButton.IsEnabled = true;
            TtsCancelButton.IsEnabled = false;
        }
    }

    private void TtsCancelClick(object sender, RoutedEventArgs e) => _ttsCts?.Cancel();

    private void TtsOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(VoiceFolder.Text))
        {
            Alert("Hãy chọn thư mục MP3.");
            return;
        }
        var path = Path.GetFullPath(VoiceFolder.Text);
        Directory.CreateDirectory(path);
        OpenPath(path);
    }

    private void SaveScriptClick(object sender, RoutedEventArgs e) => SaveScript();

    private void BrowseVoiceClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục chứa 1.mp3, 2.mp3, ..." };
        if (dlg.ShowDialog(this) == true) VoiceFolder.Text = dlg.FolderName;
    }

    private bool ValidateVoice()
    {
        if (!Directory.Exists(VoiceFolder.Text)) { Alert("Không tìm thấy thư mục MP3."); return false; }
        var segments = ScriptEditor.Text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (segments.Length == 0) { Alert("Kịch bản lời đọc đang trống."); return false; }
        var missing = Enumerable.Range(1, segments.Length).Where(i =>
            !File.Exists(Path.Combine(VoiceFolder.Text, i + ".mp3"))).ToArray();
        if (missing.Length > 0)
        { Alert("Thiếu các tệp MP3 theo thứ tự: " + string.Join(", ", missing.Take(20))); return false; }
        Log($"Kiểm tra giọng đọc thành công: {segments.Length} đoạn và các MP3 được đánh số tương ứng.");
        return true;
    }

    private void ValidateVoiceClick(object sender, RoutedEventArgs e) => ValidateVoice();

    private async void PrepareVoiceClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject() || !ValidateVoice() || !SaveScript()) return;
        await ExecuteAsync((log, ct) => _commands.PrepareVoiceAsync(_selected!.Name,
            VoiceFolder.Text, _scriptPath!, log, ct), "Chuẩn bị giọng đọc");
    }

    private async void DraftShotsClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        var timeline = _projects.FileInProject(_selected!.Name, "timeline", "lines.json");
        if (!File.Exists(timeline)) { Alert("Hãy tạo timeline lời đọc trước: thiếu timeline/lines.json."); return; }
        var shots = _projects.FileInProject(_selected.Name, "story", "shots.json");
        if (File.Exists(shots) && MessageBox.Show(this,
                "Tạo lại cảnh nháp có thể ghi đè các chỉnh sửa hiện tại. Bạn muốn tiếp tục?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (File.Exists(shots)) File.Copy(shots, shots + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
        await ExecuteAsync((log, ct) => _commands.DraftShotsAsync(_selected.Name, log, ct),
            "Tạo cảnh nháp", () =>
            {
                LoadShots();
                if (_shots is not null && _selected is not null)
                {
                    var activePack = EngineCatalog.GetProjectPack(_selected.Directory);
                    _shots.SetInitialMapDefaults(activePack);
                    ProjectFiles.SaveJson(
                        _projects.FileInProject(_selected.Name, "story", "shots.json"),
                        _shots.Root);
                    ShotsGrid.ItemsSource = _shots.Rows;
                    Log("Đã khởi tạo mẫu cảnh bản đồ và khung hình camera đầu tiên.");
                }
            });
    }

    private void OpenTimelineClick(object sender, RoutedEventArgs e)
    { if (HasProject()) OpenPath(Path.Combine(_selected!.Directory, "timeline")); }

    private void LoadShots()
    {
        if (_selected is null) return;
        var file = _projects.FileInProject(_selected.Name, "story", "shots.json");
        if (!File.Exists(file)) { _shots = null; ShotsGrid.ItemsSource = null; return; }
        _shots = new ShotDocument(ProjectFiles.ReadJson(file));
        ShotsGrid.ItemsSource = _shots.Rows;
        Log("Đã tải số cảnh: " + _shots.Shots.Count);
    }

    private void LoadShotsClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        try { LoadShots(); } catch (Exception ex) { Alert(ex.Message); }
    }

    private void ShotsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_shots == null || ShotsGrid.SelectedIndex < 0 || ShotsGrid.SelectedIndex >= _shots.Shots.Count) return;
        ArgsEditor.Text = _shots.GetArgsJson(ShotsGrid.SelectedIndex);
        var args = JsonNode.Parse(ArgsEditor.Text);
        MapViewInput.Text = args?["view"]?.ToString() ?? "";
        MapYearInput.Text = args?["year"]?.ToString() ?? "";
        ShotTemplateInput.Text = _shots.Rows[ShotsGrid.SelectedIndex].Template;
        MapPlaceInput.Text = "";
        var shot = _shots.Rows[ShotsGrid.SelectedIndex];
        MapTime.Value = Math.Clamp(shot.Start, MapTime.Minimum, MapTime.Maximum);
    }

    private void ApplyMapFieldsClick(object sender, RoutedEventArgs e)
    {
        if (_shots == null || ShotsGrid.SelectedIndex < 0)
        { Alert("Hãy chọn một cảnh trước."); return; }
        if (_selected != null &&
            !EngineCatalog.GetProjectPack(_selected.Directory).Equals("bando",
                StringComparison.OrdinalIgnoreCase))
        {
            Alert("Các trường view/year/labels nhanh chỉ dùng cho Pack Bản Đồ Sử. " +
                "Với Terrain, hãy sửa args JSON (world, cam, camFrom, do) và worlds JSON.");
            return;
        }
        try
        {
            var obj = JsonNode.Parse(ArgsEditor.Text) as JsonObject ?? new JsonObject();
            if (!string.IsNullOrWhiteSpace(MapViewInput.Text))
                obj["view"] = MapViewInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(MapYearInput.Text))
                obj["year"] = MapYearInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(MapPlaceInput.Text))
            {
                var place = MapPlaceInput.Text.Trim();
                if (obj["labels"] is not JsonArray labels)
                {
                    labels = new JsonArray();
                    obj["labels"] = labels;
                }
                labels.Add(new JsonObject
                {
                    ["text"] = place,
                    ["place"] = place,
                    ["at"] = 0.3
                });
                MapPlaceInput.Text = "";
            }
            ArgsEditor.Text = obj.ToJsonString(JsonOptions.Pretty);
            var selectedIndex = ShotsGrid.SelectedIndex;
            _shots.UpdateArgs(selectedIndex, ArgsEditor.Text);
            if (!string.IsNullOrWhiteSpace(ShotTemplateInput.Text))
                _shots.UpdateTemplate(selectedIndex, ShotTemplateInput.Text);
            ShotsGrid.ItemsSource = null; ShotsGrid.ItemsSource = _shots.Rows;
            ShotsGrid.SelectedIndex = selectedIndex;
            Log("Đã áp dụng thuộc tính bản đồ cho " + _shots.Rows[selectedIndex].Id);
        }
        catch (Exception ex) { Alert("Không thể áp dụng thuộc tính bản đồ: " + ex.Message); }
    }

    private void ApplyShotClick(object sender, RoutedEventArgs e)
    {
        if (_shots == null || ShotsGrid.SelectedIndex < 0) { Alert("Hãy chọn một cảnh trước."); return; }
        try
        {
            var index = ShotsGrid.SelectedIndex;
            _shots.UpdateArgs(index, ArgsEditor.Text);
            if (!string.IsNullOrWhiteSpace(ShotTemplateInput.Text))
                _shots.UpdateTemplate(index, ShotTemplateInput.Text);
            ShotsGrid.ItemsSource = null; ShotsGrid.ItemsSource = _shots.Rows;
            ShotsGrid.SelectedIndex = index;
            Log("Đã áp dụng tham số và template cho cảnh " + _shots.Rows[index].Id);
        }
        catch (Exception ex) { Alert("JSON của cảnh không hợp lệ: " + ex.Message); }
    }

    private void EditWorldsClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject() || _shots is null)
        {
            Alert("Hãy chọn dự án và tải cảnh trước."); return;
        }
        var root = _shots.Root;
        var original = root["worlds"]?.ToJsonString(JsonOptions.Pretty) ?? "{}";
        var dialog = new Window
        {
            Title = "Worlds JSON — " + _selected!.Name,
            Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 840, Height = 620, MinWidth = 520, MinHeight = 380
        };
        var panel = new DockPanel { Margin = new Thickness(10) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var saveButton = new Button { Content = "Áp dụng JSON worlds", Margin = new Thickness(5) };
        var cancelButton = new Button { Content = "Hủy", Margin = new Thickness(5) };
        var editor = new TextBox { Text = original, AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        saveButton.Click += (_, _) =>
        {
            try
            {
                var worlds = JsonNode.Parse(editor.Text);
                if (worlds is not JsonObject)
                    throw new JsonException("worlds phải là một JSON object.");
                root["worlds"] = worlds;
                dialog.DialogResult = true;
            }
            catch (Exception ex) { Alert("JSON worlds không hợp lệ: " + ex.Message); }
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        buttons.Children.Add(saveButton); buttons.Children.Add(cancelButton);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons); panel.Children.Add(editor);
        dialog.Content = panel;
        if (dialog.ShowDialog() == true)
            Log("Đã cập nhật worlds (chưa lưu). Nhấn Kiểm tra và lưu toàn bộ cảnh để ghi story/shots.json.");
    }

    private async void DownloadTerrainClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        if (!EngineCatalog.GetProjectPack(_selected!.Directory)
            .Equals("terrain", StringComparison.OrdinalIgnoreCase))
        {
            Alert("Tải DEM chỉ áp dụng cho dự án Terrain."); return;
        }
        var worlds = _shots?.Root["worlds"] as JsonObject;
        if (worlds is null || worlds.Count == 0)
        {
            Alert("Chưa có worlds. Hãy cấu hình world và dem.c [lon,lat] trước."); return;
        }
        if (MessageBox.Show(this,
                "Tải dữ liệu địa hình DEM thực từ nguồn Terrain Tiles cho worlds có dem.c? " +
                "Tác vụ cần kết nối Internet và có thể tải nhiều tile. " +
                "Các file đã có sẽ được cập nhật bởi tool của engine.",
                "Xác nhận tải Terrain DEM", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await ExecuteAsync((log, ct) => _commands.DownloadTerrainAsync(_selected.Name, log, ct),
            "Tải DEM Terrain");
    }

    private async void AiBuildTerrainClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            Alert("Hãy chọn dự án Terrain ở tab Dự án trước.");
            MainTabs.SelectedIndex = 0;
            return;
        }
        if (!EngineCatalog.GetProjectPack(_selected.Directory)
                .Equals("terrain", StringComparison.OrdinalIgnoreCase))
        {
            Alert("AI Terrain Scene Builder chỉ hỗ trợ dự án pack=terrain."); return;
        }
        if (_operation != null || _environmentCts != null || _aiCts != null ||
            _ttsCts != null || _terrainAiCts != null)
        {
            Alert("Có tác vụ khác đang chạy. Hãy hủy hoặc hoàn thành trước."); return;
        }
        if (_shots is null)
        {
            try { LoadShots(); }
            catch (Exception ex) { Alert(ex.Message); return; }
        }
        if (_shots is null || _shots.Shots.Count == 0)
        {
            Alert("Chưa có cảnh nháp. Hãy tạo timeline và nhấn Tạo cảnh nháp trước."); return;
        }
        if (MessageBox.Show(this, "AI sẽ đọc 03-HINH, các mẫu cảnh Terrain và gửi " +
                "kế hoạch cùng lời đọc đến nhà cung cấp AI đã chọn tại tab AI Kịch bản. " +
                "AI sẽ điền worlds, template, camera và hành động cho toàn bộ " +
                _shots.Shots.Count + " cảnh. Thời gian và lời đọc được giữ nguyên. " +
                "Kết quả chỉ được lưu sau khi bạn xác nhận. Tiếp tục?",
                "AI tạo toàn bộ cảnh Terrain 3D",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var request = BuildAiRequest();
        if (!ConfirmCustomEndpoint(request)) return;
        var project = _selected.Directory;
        var slug = _selected.Name;
        var snapshot = File.ReadAllText(Path.Combine(project, "story", "shots.json"));
        using var cts = new CancellationTokenSource();
        _terrainAiCts = cts;
        AiBuildTerrainButton.IsEnabled = false;
        AiCancelTerrainButton.IsEnabled = true;
        try
        {
            var profile = _engineCatalog.ResolveForProject(project);
            var skillRoot = GetSelectedSkillRoot();
            var assistant = new AiScriptStudio(skillRoot);
            var builder = new AiTerrainSceneBuilder(assistant);
            SaveCustomAiPreferences(request);
            AiTerrainStatus.Text = "AI Terrain đang đọc kế hoạch và chuẩn bị các cảnh...";
            var progress = new Progress<string>(message =>
            {
                AiTerrainStatus.Text = message;
                Log("AI Terrain: " + message);
            });
            var built = await builder.GenerateAsync(request, project, profile.EngineRoot,
                progress, cts.Token);
            if (_selected?.Directory != project || _selected?.Name != slug)
            {
                AiTerrainStatus.Text = "Dự án đã thay đổi. Chưa lưu kết quả AI.";
                return;
            }
            if (cts.IsCancellationRequested) return;
            var worldCount = (built.Root["worlds"] as JsonObject)?.Count ?? 0;
            if (MessageBox.Show(this, "AI đã tạo " + built.Shots.Count + " cảnh và " +
                    worldCount + " thế giới địa hình.\n\n" +
                    "Cần kiểm tra lại tính chính xác của vị trí, lịch sử và camera. " +
                    "Bản gốc sẽ được sao lưu trước khi thay thế shots.json.\n\n" +
                    "Áp dụng bản nháp AI vào dự án?",
                    "Xem trước kết quả AI Terrain", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                AiTerrainStatus.Text = "Đã từ chối áp dụng. shots.json không thay đổi.";
                return;
            }
            AiTerrainSceneBuilder.SaveWithBackup(project, built, snapshot);
            LoadShots();
            AiTerrainStatus.Text = "Đã lưu " + built.Shots.Count +
                " cảnh AI và sao lưu shots.json cũ. Hãy chạy Kiểm tra cảnh và Bảng ảnh tổng hợp.";
            Log(AiTerrainStatus.Text);
        }
        catch (OperationCanceledException)
        {
            AiTerrainStatus.Text = "Đã hủy AI Terrain. shots.json chưa được sửa.";
        }
        catch (Exception ex)
        {
            AiTerrainStatus.Text = "AI Terrain thất bại: " + ex.Message;
            Log("AI Terrain: " + ex);
            Alert(AiTerrainStatus.Text);
        }
        finally
        {
            _terrainAiCts = null;
            AiBuildTerrainButton.IsEnabled = true;
            AiCancelTerrainButton.IsEnabled = false;
        }
    }

    private void AiCancelTerrainClick(object sender, RoutedEventArgs e) =>
        _terrainAiCts?.Cancel();

    private void SaveShotsClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject() || _shots == null) return;
        try
        {
            _shots.Validate();
            var file = _projects.FileInProject(_selected!.Name, "story", "shots.json");
            if (File.Exists(file)) File.Copy(file, file + ".bak", true);
            ProjectFiles.SaveJson(file, _shots.Root);
            Log("Đã lưu các cảnh: " + file);
        }
        catch (Exception ex) { Alert("Không thể lưu các cảnh: " + ex.Message); }
    }

    private async void StartLiveClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        if (_previewHost != null && !_previewHost.HasExited) { Alert("Bản xem trước đang chạy."); return; }
        try
        {
            var script = Path.Combine(AppContext.BaseDirectory, "integration", "preview-host.mjs");
            var activeEngine = _engineCatalog.ResolveForProject(_selected!.Directory);
            var psi = new ProcessStartInfo(_settings.NodeExecutable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = activeEngine.EngineRoot
            };
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add("--studio");
            psi.ArgumentList.Add(activeEngine.EngineRoot);
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(_selected!.Name);
            psi.ArgumentList.Add("--project-dir");
            psi.ArgumentList.Add(_selected.Directory);
            _previewHost = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _previewHost.OutputDataReceived += (_, args) =>
            {
                if (args.Data?.StartsWith("PREVIEW_URL ", StringComparison.Ordinal) == true)
                    ready.TrySetResult(args.Data["PREVIEW_URL ".Length..]);
                else if (args.Data != null) Log(args.Data);
            };
            _previewHost.ErrorDataReceived += (_, args) => { if (args.Data != null) Log("[xem trước] " + args.Data); };
            _previewHost.Exited += (_, _) => ready.TrySetException(new InvalidOperationException("Máy chủ xem trước đã thoát trước khi khởi động."));
            _previewHost.Start();
            _previewHost.BeginOutputReadLine();
            _previewHost.BeginErrorReadLine();
            _previewUrl = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await MapBrowser.EnsureCoreWebView2Async();
            MapBrowser.Source = new Uri(_previewUrl);
            try
            {
                var cfg = ProjectFiles.ReadJson(_projects.FileInProject(_selected.Name, "story", "shots.json"));
                MapTime.Maximum = Math.Max(1, double.TryParse(cfg["duration"]?.ToString(), out var dur) ? dur : 60);
            }
            catch { MapTime.Maximum = 60; }
            LiveStatus.Text = "Đang chạy — " + _selected.Name;
            Log("Bản đồ trực tiếp: " + _previewUrl);
        }
        catch (Exception ex) { StopPreviewHost(); Alert("Không thể khởi động bản đồ xem trước: " + ex.Message); }
    }

    private async void SeekLiveClick(object sender, RoutedEventArgs e)
    {
        if (MapBrowser.CoreWebView2 == null) return;
        var t = MapTime.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            await MapBrowser.ExecuteScriptAsync(
                $"if (window.__ready && window.__info) window.renderFrame(Math.max(0, Math.round({t} * window.__info.fps)));");
        }
        catch (Exception ex) { Log("Lỗi chuyển thời điểm: " + ex.Message); }
    }

    private void RefreshLiveClick(object sender, RoutedEventArgs e)
    {
        if (_previewUrl == null || MapBrowser.CoreWebView2 == null) return;
        MapBrowser.CoreWebView2.Navigate(_previewUrl + "&v=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private void StopLiveClick(object sender, RoutedEventArgs e) => StopPreviewHost();

    private void StopPreviewHost()
    {
        try
        {
            if (_previewHost != null && !_previewHost.HasExited)
                _previewHost.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        finally { _previewHost?.Dispose(); _previewHost = null; _previewUrl = null; LiveStatus.Text = "Đã dừng"; }
    }

    private async Task Render(RenderMode mode, string label, Action? success = null)
    {
        if (!HasProject()) return;
        await ExecuteAsync((log, ct) => _commands.RenderAsync(_selected!.Name, mode, log, ct), label, success);
    }

    private async void CheckRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Check, "Kiểm tra cảnh");
    private async void SheetRenderClick(object sender, RoutedEventArgs e) =>
        await Render(RenderMode.Sheet, "Bảng ảnh tổng hợp", () =>
        {
            var directory = Path.Combine(_selected!.Directory, "build", "sheets");
            if (EngineCatalog.GetProjectPack(_selected.Directory)
                .Equals("terrain", StringComparison.OrdinalIgnoreCase))
            {
                var count = _shots?.Shots.Count ?? 0;
                var findings = TerrainSheetQualityAnalyzer.Analyze(directory, count);
                if (findings.Count == 0)
                    Log("Terrain image QC: không có cảnh báo tương phản thấp từ ảnh tổng hợp " +
                        "(đây không phải AI Vision hoặc xác minh nội dung lịch sử).");
                else
                {
                    Log("Terrain image QC: " + findings.Count + " cảnh báo từ ảnh tổng hợp:");
                    foreach (var finding in findings) Log("  [IMAGE QC] " + finding);
                }
            }
            OpenPath(directory);
        });
    private async void PreviewRenderClick(object sender, RoutedEventArgs e) =>
        await Render(RenderMode.Preview, "Xem thử 30 giây", () =>
        {
            var p = Path.Combine(_selected!.Directory, "build", "preview.mp4");
            if (File.Exists(p)) { VideoPlayer.Source = new Uri(p); VideoPlayer.Play(); }
        });
    private async void FullRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Full, "Dựng toàn bộ video");
    private void CreateThumbSceneClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        var path = _projects.FileInProject(_selected!.Name, "scenes", "shots", "thumb.js");
        if (File.Exists(path) && MessageBox.Show(this, "Thay thế cảnh ảnh bìa hiện tại?",
                "Xác nhận ghi đè", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            var view = JsonSerializer.Serialize(ThumbViewInput.Text.Trim());
            var first = JsonSerializer.Serialize(ThumbLineOne.Text.Trim());
            var second = JsonSerializer.Serialize(ThumbLineTwo.Text.Trim());
            var source = "import { thumb } from '/packs/bando/pack.js';\\n" +
                $"thumb({{ view: {view}, lines: [{first}, {second}], colors: ['white', 'yellow'], pos: 'br' }});\\n";
            ProjectFiles.SaveText(path, source.Replace("\\n", Environment.NewLine));
            Log("Đã tạo cảnh ảnh bìa: " + path);
        }
        catch (Exception ex) { Alert("Không thể tạo mã ảnh bìa: " + ex.Message); }
    }

    private async void ThumbnailRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Thumbnail,
        "Ảnh bìa", () => { OpenPath(Path.Combine(_selected!.Directory, "out")); _ = PublishExportAsync(); });

    private async void MixRenderClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        var video = Path.Combine(_selected!.Directory, "build", "video_silent.mp4");
        if (!File.Exists(video)) { Alert("Hãy hoàn tất dựng video trước khi ghép âm thanh."); return; }
        await ExecuteAsync((log, ct) => _commands.MixAsync(_selected.Name, log, ct), "Ghép âm thanh", () =>
            { OpenPath(Path.Combine(_selected.Directory, "out")); _ = PublishExportAsync(); });
    }

    private void PlayVideoClick(object sender, RoutedEventArgs e) => VideoPlayer.Play();
    private void PauseVideoClick(object sender, RoutedEventArgs e) => VideoPlayer.Pause();
    private void VideoPlayerFailed(object sender, ExceptionRoutedEventArgs e) => Log("Lỗi phát video: " + e.ErrorException.Message);
    private void OpenOutputClick(object sender, RoutedEventArgs e)
    { if (HasProject()) OpenPath(Path.Combine(_selected!.Directory, "out")); }

    private static void OpenPath(string path)
    {
        if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void BrowseStudioClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục H2 Studio (chứa render.mjs)" };
        if (dlg.ShowDialog(this) == true) StudioRootText.Text = dlg.FolderName;
    }

    private void BrowseWorkspaceClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn Workspace chứa các dự án" };
        if (dlg.ShowDialog(this) == true) WorkspaceRootText.Text = dlg.FolderName;
    }

    private void BrowseExportsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục xuất video" };
        if (dlg.ShowDialog(this) == true) ExportRootText.Text = dlg.FolderName;
    }

    private async Task PublishExportAsync()
    {
        if (!HasProject()) return;
        var name = _selected!.Name;
        try
        {
            var destination = await Task.Run(() => new ExportPublisher(_settings).Publish(name));
            Log("Đã xuất thành phẩm sang: " + destination);
            RenderStatus.Text = "Đã sao chép sang thư mục xuất";
        }
        catch (Exception ex)
        {
            Log("Lỗi xuất thành phẩm: " + ex.Message);
            Alert("Không thể sao chép video sang thư mục xuất: " + ex.Message);
        }
    }

    private async void PublishExportClick(object sender, RoutedEventArgs e) => await PublishExportAsync();

    private void OpenExportClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        OpenPath(Path.Combine(_settings.ExportRoot, _selected!.Name));
    }

    private void RefreshPortableStatus()
    {
        var portable = new PortableStudioPaths();
        PortableStatus.Text = portable.HasEngine
            ? "Engine Portable đã sẵn sàng tại " + portable.Engine
            : "Chưa có engine đầy đủ tại " + portable.Engine +
              ". Hãy nhập thư mục H2 gốc bằng nút Nhập engine. Tạo thư mục studio rỗng không đủ để dựng video.";
    }

    private void UsePortableLayoutClick(object sender, RoutedEventArgs e)
    {
        var portable = new PortableStudioPaths();
        StudioRootText.Text = portable.Engine;
        WorkspaceRootText.Text = portable.Projects;
        ExportRootText.Text = portable.Exports;
        RefreshPortableStatus();
        Log("Đã chọn cấu trúc Portable; nhấn Lưu cài đặt để áp dụng.");
    }

    private void OpenPortableRootClick(object sender, RoutedEventArgs e)
        => OpenPath(new PortableStudioPaths().Root);

    private async void ImportEngineClick(object sender, RoutedEventArgs e)
    {
        if (_operation != null || _environmentCts != null)
        {
            Alert("Đang có tác vụ xử lý; hãy hoàn tất trước khi nhập engine.");
            return;
        }
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục h2-2d-studio gốc (chứa render.mjs)" };
        if (dlg.ShowDialog(this) != true) return;
        var paths = new PortableStudioPaths();
        if (MessageBox.Show(this,
            "Sao chép mã nguồn và tài nguyên H2 vào:\\n" + paths.Engine +
            "\\n\\nEngine nguồn không bị xóa. Bỏ qua node_modules, .venv và projects; cần cài thư viện trong Quản lý môi trường.",
            "Xác nhận nhập engine", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        using var cts = new CancellationTokenSource();
        _operation = cts;
        try
        {
            RenderStatus.Text = "Đang nhập engine Portable...";
            await paths.ImportEngineAsync(dlg.FolderName, Log, cts.Token);
            if (string.IsNullOrWhiteSpace(_settings.LegacyEngineRoot) &&
                !Path.GetFullPath(dlg.FolderName).Equals(paths.Engine, StringComparison.OrdinalIgnoreCase))
                _settings.LegacyEngineRoot = dlg.FolderName;
            _settings.UsePortablePaths();
            _settings.Save();
            _projects = new ProjectFiles(_settings);
            _commands = new StudioCommands(_settings);
            _engineCatalog = new EngineCatalog(_settings);
            RefreshPacks();
            _selected = null;
            FillSettings();
            RefreshProjects();
            RenderStatus.Text = "Nhập engine Portable thành công";
            Alert("Đã nhập engine. Tiếp theo mở Quản lý môi trường để cài các dependency còn thiếu.");
        }
        catch (OperationCanceledException) { RenderStatus.Text = "Đã hủy nhập engine"; }
        catch (Exception ex)
        {
            RenderStatus.Text = "Nhập engine thất bại";
            Log("Nhập engine thất bại: " + ex);
            Alert(ex.Message);
        }
        finally { _operation = null; RefreshPortableStatus(); }
    }

    private void SaveSettingsClick(object sender, RoutedEventArgs e)
    {
        var proposed = new StudioSettings
        {
            StudioRoot = StudioRootText.Text.Trim(),
            WorkspaceRoot = WorkspaceRootText.Text.Trim(),
            ExportRoot = ExportRootText.Text.Trim(),
            LegacyEngineRoot = _settings.LegacyEngineRoot,
            LegacyWorkspaceRoot = _settings.LegacyWorkspaceRoot,
            // Saving filesystem settings must not erase AI or voice profiles.
            CustomAiBaseUrl = _settings.CustomAiBaseUrl,
            CustomAiModel = _settings.CustomAiModel,
            CustomAiEndpoint = _settings.CustomAiEndpoint,
            TtsVoiceIds = (_settings.TtsVoiceIds ?? []).ToList(),
            TtsSelectedVoiceId = _settings.TtsSelectedVoiceId ?? "",
            NodeExecutable = NodeText.Text.Trim(),
            PythonExecutable = PythonText.Text.Trim(),
            FfmpegExecutable = FfmpegText.Text.Trim()
        };
        if (string.IsNullOrWhiteSpace(proposed.StudioRoot) ||
            string.IsNullOrWhiteSpace(proposed.WorkspaceRoot) ||
            string.IsNullOrWhiteSpace(proposed.ExportRoot))
        {
            Alert("Các đường dẫn không được để trống."); return;
        }
        var portablePaths = new PortableStudioPaths();
        if (!_settings.IsPortable &&
            Path.GetFullPath(proposed.StudioRoot).Equals(portablePaths.Engine, StringComparison.OrdinalIgnoreCase))
        {
            if (!Path.GetFullPath(_settings.StudioRoot).Equals(portablePaths.Engine, StringComparison.OrdinalIgnoreCase))
                proposed.LegacyEngineRoot = _settings.StudioRoot;
            if (!Path.GetFullPath(_settings.WorkspaceRoot).Equals(portablePaths.Projects, StringComparison.OrdinalIgnoreCase))
                proposed.LegacyWorkspaceRoot = _settings.WorkspaceRoot;
        }
        try { ProjectFiles.ValidateRoots(proposed); }
        catch (Exception ex) { Alert("Đường dẫn cấu hình không hợp lệ: " + ex.Message); return; }
        StopPreviewHost();
        _settings = proposed;
        _settings.Save();
        _projects = new ProjectFiles(_settings);
        _commands = new StudioCommands(_settings);
        _engineCatalog = new EngineCatalog(_settings);
        _selected = null;
        RefreshPacks();
        RefreshProjects();
        _environmentRows.Clear();
        EnvironmentProgressBar.Value = 0;
        EnvironmentStatus.Text = "Cấu hình đã thay đổi. Hãy kiểm tra lại môi trường.";
        RefreshPortableStatus();
        if (!File.Exists(Path.Combine(_settings.StudioRoot, "render.mjs")))
            Log("CẢNH BÁO: Chưa có engine đầy đủ; hãy nhập engine H2 trước khi tạo dự án.");
        Log("Đã lưu cài đặt tại " + StudioSettings.SettingsPath);
    }

    private async void DoctorClick(object sender, RoutedEventArgs e)
    {
        if (!HasStudio()) return;
        var engineRoot = EnvironmentEngineRoot;
        await ExecuteAsync((log, ct) => _commands.DoctorAsync(log, ct, engineRoot),
            "Kiểm tra môi trường: " + engineRoot);
    }


    private void AiProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AiModelInput is null || AiProviderInput is null ||
            AiCustomOptions is null || AiKeyInput is null) return;
        var provider = (AiProviderInput.SelectedItem as ComboBoxItem)?.Tag as string;
        AiCustomOptions.Visibility = provider == "Custom"
            ? Visibility.Visible : Visibility.Collapsed;
        AiModelInput.Text = provider switch
        {
            "Gemini" => "gemini-2.5-flash",
            "Custom" => string.IsNullOrWhiteSpace(_settings.CustomAiModel)
                ? "your-model-id" : _settings.CustomAiModel,
            _ => "gpt-5.5"
        };
        // Keys are intentionally not carried between providers.
        AiKeyInput.Clear();
    }

    private AiScriptRequest BuildAiRequest()
    {
        var tag = (AiProviderInput.SelectedItem as ComboBoxItem)?.Tag as string;
        var provider = tag switch
        {
            "Gemini" => AiScriptProvider.Gemini,
            "Custom" => AiScriptProvider.Custom,
            _ => AiScriptProvider.OpenAI
        };
        var duration = int.TryParse((AiDurationInput.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            out var minutes) ? minutes : 12;
        var endpoint = (AiCustomEndpointInput.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Responses"
            ? AiCustomEndpoint.Responses : AiCustomEndpoint.ChatCompletions;
        return new AiScriptRequest(provider, AiModelInput.Text.Trim(), AiKeyInput.Password,
            AiTopicInput.Text.Trim(), duration,
            provider == AiScriptProvider.Custom ? AiCustomBaseUrl.Text.Trim() : null,
            endpoint);
    }

    private bool ConfirmCustomEndpoint(AiScriptRequest request)
    {
        if (request.Provider != AiScriptProvider.Custom) return true;
        Uri endpoint;
        try { endpoint = AiEndpoint.Resolve(request.BaseUrl ?? "", request.CustomEndpoint); }
        catch (ArgumentException ex) { Alert(ex.Message); return false; }
        if (endpoint.Scheme == Uri.UriSchemeHttp && !endpoint.IsLoopback)
        {
            return MessageBox.Show(this,
                "Bạn đang kết nối đến API Custom qua HTTP không mã hóa:\n" +
                endpoint.Host +
                "\n\nNội dung kịch bản và API key (nếu có) có thể bị đọc trên đường truyền. " +
                "Nên dùng HTTPS. Bạn có thực sự muốn tiếp tục?",
                "Cảnh báo kết nối Custom không an toàn",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
        return true;
    }

    private void SaveCustomAiPreferences(AiScriptRequest request)
    {
        if (request.Provider != AiScriptProvider.Custom) return;
        // Never persist the API key: only base URL, model and endpoint type.
        _settings.CustomAiBaseUrl = request.BaseUrl ?? "";
        _settings.CustomAiModel = request.Model;
        _settings.CustomAiEndpoint = request.CustomEndpoint;
        _settings.Save();
    }

    private async void AiTestConnectionClick(object sender, RoutedEventArgs e)
    {
        if (_aiCts != null || _operation != null || _environmentCts != null)
        {
            Alert("Đang có tác vụ khác chạy. Hãy hoàn tất hoặc hủy trước.");
            return;
        }
        var request = BuildAiRequest();
        if (!ConfirmCustomEndpoint(request)) return;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        _aiCts = cts;
        AiTestButton.IsEnabled = false;
        AiRunButton.IsEnabled = false;
        AiCancelButton.IsEnabled = true;
        try
        {
            var skill = GetSelectedSkillRoot();
            var studio = new AiScriptStudio(skill);
            AiStatus.Text = "Đang thử kết nối đến API Custom...";
            var result = await studio.TestConnectionAsync(request, cts.Token);
            SaveCustomAiPreferences(request);
            AiStatus.Text = result;
            Log("AI: " + result);
        }
        catch (OperationCanceledException)
        {
            AiStatus.Text = "Đã hủy hoặc hết thời gian thử kết nối.";
        }
        catch (Exception ex)
        {
            AiStatus.Text = "Kết nối AI thất bại: " + ex.Message;
            Log("AI: " + AiStatus.Text);
        }
        finally
        {
            _aiCts = null;
            AiTestButton.IsEnabled = true;
            AiRunButton.IsEnabled = true;
            AiCancelButton.IsEnabled = false;
        }
    }

    private string AiPath(string slug, string prefix, string ext) =>
        _projects.FileInProject(slug, prefix + "-" + slug + "." + ext);

    private string SelectedPack =>
        _selected is null ? "bando" : EngineCatalog.GetProjectPack(_selected.Directory);

    private string PlanFilePrefix => SelectedPack.Equals("terrain", StringComparison.OrdinalIgnoreCase)
        ? "03-HINH" : "03-BAN-DO";

    private string GetSelectedSkillRoot()
    {
        if (_selected is null) throw new InvalidOperationException("Chưa chọn dự án.");
        var profile = _engineCatalog.ResolveForProject(_selected.Directory);
        return AiScriptStudio.ResolveSkillRoot(AppContext.BaseDirectory,
            profile.EngineRoot, profile.Pack);
    }

    private void LoadAiFiles()
    {
        if (_selected is null) return;
        var slug = _selected.Name;
        AiScriptEditor.Text = LoadIfExists(AiPath(slug, "01-SCRIPT", "txt"));
        AiVoiceEditor.Text = LoadIfExists(AiPath(slug, "02-VOICE", "txt"));
        AiMapEditor.Text = LoadIfExists(AiPath(slug, PlanFilePrefix, "md"));
        AiPackageEditor.Text = LoadIfExists(AiPath(slug, "05-PACKAGE", "md"));
        AiStatus.Text = "Đang mở dự án: " + _selected.Title + " / Pack: " + SelectedPack;
    }

    private static string LoadIfExists(string path) =>
        File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;

    private async void AiRunClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            Alert("Hãy tạo/chọn dự án ở tab Dự án trước khi viết kịch bản AI.");
            MainTabs.SelectedIndex = 0;
            return;
        }
        if (_operation != null || _environmentCts != null || _aiCts != null)
        {
            Alert("Đang có tác vụ khác chạy, hãy hoàn thành hoặc hủy trước.");
            return;
        }
        var request = BuildAiRequest();
        if (!ConfirmCustomEndpoint(request)) return;
        var slug = _selected.Name;
        var skill = GetSelectedSkillRoot();
        using var cts = new CancellationTokenSource();
        _aiCts = cts;
        AiRunButton.IsEnabled = false;
        AiCancelButton.IsEnabled = true;
        try
        {
            var client = new AiScriptStudio(skill);
            if (!client.HasSkill)
                throw new FileNotFoundException("Pack này chưa có skill AI tương ứng. Cần SKILL.md và references/cau-truc.md tại " + skill);
            var progress = new Progress<string>(message =>
            {
                AiStatus.Text = message;
                Log("AI: " + message);
            });
            SaveCustomAiPreferences(request);
            var bundle = await client.GenerateAsync(request, slug, progress, cts.Token);
            if (_selected?.Name != slug)
            {
                AiStatus.Text = "Dự án đã thay đổi trong khi AI tạo nội dung. Kết quả không được ghi đè dự án khác.";
                return;
            }
            AiScriptEditor.Text = bundle.Script;
            AiVoiceEditor.Text = bundle.Voice;
            AiMapEditor.Text = bundle.MapPlan;
            AiPackageEditor.Text = bundle.Package;
            AiStatus.Text = $"Đã tạo bản nháp cho {slug}: {bundle.WordCount} chữ, {bundle.VoiceSegments} đoạn voice. Kiểm tra nội dung và nguồn, sau đó lưu 4 file.";
            Log("AI đã tạo bản nháp. Chưa lưu vào ổ đĩa và chưa tạo MP3.");
        }
        catch (OperationCanceledException)
        {
            AiStatus.Text = "Đã hủy tạo kịch bản. Nội dung cũ chưa bị thay đổi.";
        }
        catch (Exception ex)
        {
            AiStatus.Text = "Tạo kịch bản thất bại: " + ex.Message;
            Log("Lỗi AI: " + ex.Message);
            Alert("Không thể tạo kịch bản AI: " + ex.Message);
        }
        finally
        {
            _aiCts = null;
            AiRunButton.IsEnabled = true;
            AiCancelButton.IsEnabled = false;
        }
    }

    private void AiCancelClick(object sender, RoutedEventArgs e) => _aiCts?.Cancel();

    private void AiSaveClick(object sender, RoutedEventArgs e)
    {
        if (_aiCts != null)
        {
            Alert("AI đang viết; hãy hoàn thành hoặc hủy trước khi lưu.");
            return;
        }
        if (_selected is null)
        {
            Alert("Hãy chọn dự án trước.");
            return;
        }
        try
        {
            AiScriptStudio.ValidateBundle(AiScriptEditor.Text, AiVoiceEditor.Text,
                AiMapEditor.Text, AiPackageEditor.Text);
            var slug = _selected.Name;
            var files = new[]
            {
                (AiPath(slug, "01-SCRIPT", "txt"), AiScriptEditor.Text),
                (AiPath(slug, "02-VOICE", "txt"), AiVoiceEditor.Text),
                (AiPath(slug, "03-BAN-DO", "md"), AiMapEditor.Text),
                (AiPath(slug, "05-PACKAGE", "md"), AiPackageEditor.Text)
            };
            // Preserve previous drafts when the user generates/edits again.
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            foreach (var (file, content) in files)
            {
                if (File.Exists(file)) File.Copy(file, file + ".backup-" + stamp, true);
                ProjectFiles.SaveText(file, content);
            }
            AiStatus.Text = "Đã lưu bốn file AI vào: " + _selected.Directory;
            Log(AiStatus.Text);
        }
        catch (Exception ex)
        {
            Alert("Không lưu được các file AI: " + ex.Message);
        }
    }

    private void AiImportVoiceClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            Alert("Hãy chọn dự án trước.");
            return;
        }
        if (_aiCts != null)
        {
            Alert("Hãy chờ AI hoàn tất trước khi nhập voice.");
            return;
        }
        try
        {
            if (string.IsNullOrWhiteSpace(AiVoiceEditor.Text))
                throw new InvalidDataException("Chưa có file 02-VOICE.");
            if (!AiScriptStudio.EquivalentNarration(AiScriptEditor.Text, AiVoiceEditor.Text))
                throw new InvalidDataException("Lời đọc ở 01 và 02 khác nhau; hãy kiểm tra trước khi nhập.");
            var path = _projects.FileInProject(_selected.Name, "input", "02-VOICE.txt");
            if (File.Exists(path) &&
                MessageBox.Show(this,
                    "Đã có input/02-VOICE.txt. Sao lưu và thay bằng lời đọc AI hiện tại? " +
                    "Các tệp MP3 cũ có thể không còn phù hợp với đoạn voice mới.",
                    "Thay lời đọc dự án", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            if (File.Exists(path))
                File.Copy(path, path + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), true);
            ProjectFiles.SaveText(path, AiVoiceEditor.Text);
            ScriptEditor.Text = AiVoiceEditor.Text;
            _scriptPath = path;
            MainTabs.SelectedIndex = 2; // Kịch bản và giọng đọc
            AiStatus.Text = "Đã chuyển lời đọc vào input/02-VOICE.txt. Hãy tạo 1.mp3, 2.mp3... theo số đoạn trước khi tạo timeline.";
            Log(AiStatus.Text);
        }
        catch (Exception ex)
        {
            Alert("Không thể nhập voice: " + ex.Message);
        }
    }

    private void OpenEnvironmentClick(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 7; // Quản lý môi trường
    }

    private void SetEnvironmentBusy(bool busy)
    {
        EnvCheckButton.IsEnabled = !busy;
        EnvInstallSelectedButton.IsEnabled = !busy;
        EnvInstallMissingButton.IsEnabled = !busy;
        EnvRepairButton.IsEnabled = !busy;
        EnvCancelButton.IsEnabled = busy;
    }

    private async Task RunEnvironmentJobAsync(Func<CancellationToken, Task> operation)
    {
        if (_environmentCts != null || _operation != null)
        {
            Alert("Đang có tác vụ khác chạy. Hãy chờ hoặc hủy tác vụ hiện tại.");
            return;
        }
        using var cts = new CancellationTokenSource();
        _environmentCts = cts;
        SetEnvironmentBusy(true);
        try
        {
            await operation(cts.Token);
        }
        catch (OperationCanceledException)
        {
            EnvironmentStatus.Text = "Đã hủy thao tác môi trường.";
            Log("ĐÃ HỦY: quản lý môi trường.");
        }
        catch (Exception ex)
        {
            EnvironmentStatus.Text = "Thao tác môi trường thất bại.";
            Log("Lỗi môi trường: " + ex);
            Alert("Không thể hoàn tất thao tác: " + ex.Message);
        }
        finally
        {
            _environmentCts = null;
            SetEnvironmentBusy(false);
        }
    }

    private async Task<IReadOnlyList<DependencyResult>> CheckEnvironmentAsync(CancellationToken ct)
    {
        var manager = new EnvironmentManager(_settings, EnvironmentEngineRoot);
        var results = new List<DependencyResult>();
        _environmentRows.Clear();
        var kinds = EnvironmentManager.InstallationOrder;
        for (var i = 0; i < kinds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var kind = kinds[i];
            EnvironmentStatus.Text = "Đang kiểm tra: " + EnvironmentManager.DisplayName(kind);
            var item = await manager.CheckAsync(kind, ct);
            results.Add(item);
            _environmentRows.Add(new EnvironmentRow(item.Kind, item.Name,
                item.Installed ? "Đã cài" : "Thiếu / lỗi", item.Details));
            EnvironmentProgressBar.Value = (i + 1) * 100.0 / kinds.Count;
        }
        EnvironmentStatus.Text = $"Kiểm tra hoàn tất: {results.Count(x => x.Installed)}/{results.Count} thành phần đạt.";
        return results;
    }

    private async void EnvCheckClick(object sender, RoutedEventArgs e) =>
        await RunEnvironmentJobAsync(async ct => { await CheckEnvironmentAsync(ct); });

    private async void EnvInstallSelectedClick(object sender, RoutedEventArgs e)
    {
        if (EnvironmentGrid.SelectedItem is not EnvironmentRow selected)
        {
            Alert("Hãy chọn một thành phần trong danh sách trước.");
            return;
        }
        await RunEnvironmentJobAsync(async ct =>
        {
            var manager = new EnvironmentManager(_settings, EnvironmentEngineRoot);
            var item = await manager.CheckAsync(selected.Kind, ct);
            await InstallEnvironmentAsync([item], ct, allowInstalled: true);
        });
    }

    private async void EnvInstallMissingClick(object sender, RoutedEventArgs e) =>
        await RunEnvironmentJobAsync(async ct =>
        {
            var results = await CheckEnvironmentAsync(ct);
            await InstallEnvironmentAsync(results.Where(x => !x.Installed).ToArray(), ct);
        });

    private async void EnvRepairClick(object sender, RoutedEventArgs e) =>
        await RunEnvironmentJobAsync(async ct =>
        {
            var results = await CheckEnvironmentAsync(ct);
            // Repair only missing dependencies. Already installed system runtimes
            // are never upgraded or replaced without an explicit single-item choice.
            await InstallEnvironmentAsync(results.Where(x => !x.Installed).ToArray(), ct);
        });

    private async Task InstallEnvironmentAsync(
        IReadOnlyList<DependencyResult> candidates, CancellationToken ct, bool allowInstalled = false)
    {
        var selected = candidates.Where(x => x.CanInstall && (allowInstalled || !x.Installed)).ToArray();
        if (selected.Length == 0)
        {
            EnvironmentStatus.Text = "Không có thành phần cần cài. Kiểm tra cấu hình engine nếu còn cảnh báo.";
            return;
        }
        var summary = string.Join(Environment.NewLine, selected.Select(x =>
            "• " + x.Name + (EnvironmentManager.WinGetId(x.Kind) is string id ? " — WinGet: " + id : "")));
        if (MessageBox.Show(this,
                "Bạn xác nhận cài đặt/sửa chữa các thành phần sau?" + Environment.NewLine +
                summary + Environment.NewLine + Environment.NewLine +
                "Một số bộ cài có thể yêu cầu quyền quản trị hoặc tải dữ liệu từ Internet. " +
                "Gói hệ thống không tự động bị gỡ hoặc nâng cấp khi kiểm tra.",
                "Xác nhận cài đặt môi trường", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        var manager = new EnvironmentManager(_settings, EnvironmentEngineRoot);
        var installedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();
        for (var i = 0; i < selected.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = selected[i];
            var packageId = EnvironmentManager.WinGetId(item.Kind);
            if (packageId != null && !installedPackages.Add(packageId))
            {
                Log("Bỏ qua bộ cài trùng: " + packageId);
                continue;
            }
            EnvironmentStatus.Text = "Đang cài: " + item.Name;
            EnvironmentProgressBar.Value = i * 100.0 / selected.Length;
            try
            {
                var result = await manager.InstallAsync(item.Kind, Log, ct);
                if (result.ExitCode != 0)
                {
                    failures.Add(item.Name);
                    Log($"CÀI ĐẶT THẤT BẠI: {item.Name}, mã thoát {result.ExitCode}");
                }
                else Log("HOÀN TẤT CÀI ĐẶT: " + item.Name);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failures.Add(item.Name);
                Log("Không thể cài " + item.Name + ": " + ex.Message);
            }
            EnvironmentProgressBar.Value = (i + 1) * 100.0 / selected.Length;
        }

        await CheckEnvironmentAsync(ct);
        EnvironmentStatus.Text = failures.Count == 0
            ? "Đã chạy xong bộ cài. Nếu runtime mới chưa được nhận diện, hãy khởi động lại ứng dụng."
            : "Có " + failures.Count + " mục cài không thành công: " + string.Join(", ", failures);
    }

    private void EnvCancelClick(object sender, RoutedEventArgs e) => _environmentCts?.Cancel();

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        _operation?.Cancel();
        _environmentCts?.Cancel();
        _ttsCts?.Cancel();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        _operation?.Cancel();
        _worldCts?.Cancel();
        _environmentCts?.Cancel();
        _aiCts?.Cancel();
        _terrainAiCts?.Cancel();
        _ttsCts?.Cancel();
        StopPreviewHost();
        VideoPlayer.Stop();
    }
    private CancellationTokenSource? _worldCts;
    private string? _worldMp4;

    private string ResolveWorldEngine()
    {
        var path = WorldEnginePath.Text.Trim();
        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
        // The engine lives under the checked-out repository, not the portable studio root.
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(Path.Combine(candidate, "tools", "v6_4_pipeline.py"))) return candidate;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy engine V6.4. Hãy nhập đường dẫn tuyệt đối.");
    }

    private int WorldProjectMinutes()
    {
        var minutes = int.TryParse((AiDurationInput.SelectedItem as ComboBoxItem)?.Tag?.ToString(),
            out var value) ? value : 12;
        if (minutes < 1 || minutes > 240) throw new ArgumentOutOfRangeException(nameof(minutes));
        return minutes;
    }

    private void WorldDurationChanged(object sender, RoutedEventArgs e)
    {
        if (WorldFrames == null || WorldFps == null || WorldUseProjectDuration == null) return;
        WorldFrames.IsReadOnly = WorldUseProjectDuration.IsChecked == true;
        if (WorldUseProjectDuration.IsChecked == true)
        {
            if (!int.TryParse(WorldFps.Text, out var fps) || fps < 1) fps = 24;
            WorldFrames.Text = checked(WorldProjectMinutes() * 60 * fps).ToString();
        }
    }

    private async void WorldTimelineClick(object sender, RoutedEventArgs e)
    {
        if (_worldCts != null) { Alert("Đang có tác vụ Historical World."); return; }
        try
        {
            var engine = ResolveWorldEngine();
            var output = WorldOutputPath.Text.Trim();
            if (!Path.IsPathRooted(output)) output = Path.Combine(engine, output);
            var draft = Path.Combine(output, "v6_4_scene_plan_ai_draft.json");
            if (!File.Exists(draft)) throw new FileNotFoundException("Hãy tạo AI Scene Plan trước.", draft);
            if (!int.TryParse(WorldFps.Text, out var fps) || fps < 1)
                throw new ArgumentException("FPS không hợp lệ.");
            var timeline = Path.Combine(output, "v6_4_timeline.json");
            var start = new ProcessStartInfo(_settings.PythonExecutable)
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = engine
            };
            foreach (var arg in new[] { Path.Combine(engine, "tools", "v6_4_audio_timeline.py"),
                "--plan", draft, "--minutes", WorldProjectMinutes().ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--fps", fps.ToString(), "--output", timeline }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được Python.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var err = await stderr;
            if (process.ExitCode != 0) throw new InvalidOperationException(err);
            Log(await stdout);
            WorldDurationChanged(sender, e);
            WorldStatus.Text = "Timeline: audio ưu tiên, text dự phòng — chưa có scene compiler";
            Alert("Đã tạo timeline theo thời lượng dự án: " + timeline +
                  "\\nChưa thể render 24 cảnh AI: cần scene compiler và duyệt lịch sử.");
        }
        catch (Exception ex) { WorldStatus.Text = "Timeline thất bại"; Log(ex.ToString()); Alert(ex.Message); }
    }

    private async void WorldRenderClick(object sender, RoutedEventArgs e)
    {
        if (_worldCts != null || _operation != null) { Alert("Đang có tác vụ khác chạy."); return; }
        _worldCts = new CancellationTokenSource();
        WorldRenderButton.IsEnabled = false;
        _worldMp4 = null;
        try
        {
            var engine = ResolveWorldEngine();
            var scene = WorldScenePath.Text.Trim();
            if (!Path.IsPathRooted(scene)) scene = Path.Combine(engine, scene);
            var output = WorldOutputPath.Text.Trim();
            if (!Path.IsPathRooted(output)) output = Path.Combine(engine, output);
            if (WorldUseProjectDuration.IsChecked == true)\n            {\n                WorldDurationChanged(sender, e);\n                throw new InvalidOperationException("Chế độ theo thời lượng dự án chỉ tạo timeline, chưa render được Scene Plan đa cảnh. Bấm Đồng bộ timeline Scene Plan; muốn test một cảnh, bỏ chọn Theo thời lượng dự án.");
            }
            if (!int.TryParse(WorldFrames.Text, out var frameCount) || frameCount < 2 ||
                !int.TryParse(WorldFps.Text, out var fps) || fps < 1)
                throw new ArgumentException("Frames >= 2 và FPS >= 1.");
            var pipeline = Path.Combine(engine, "tools", "v6_4_pipeline.py");
            var service = new H2.HistoricalWorld.HistoricalWorldRenderService();
            service.Log += Log;
            service.Progress += (stage, state) => Dispatcher.InvokeAsync(() => WorldStatus.Text = stage + ": " + state);
            WorldStatus.Text = "Đang chạy...";
            _worldMp4 = await service.RenderAsync(_settings.PythonExecutable, pipeline,
                Path.GetFullPath(scene), Path.GetFullPath(output), WorldBlenderPath.Text.Trim(),
                frameCount, fps, _worldCts.Token);
            WorldStatus.Text = "Hoàn tất";
            Log("Historical World MP4: " + _worldMp4);
        }
        catch (OperationCanceledException) { WorldStatus.Text = "Đã hủy"; }
        catch (Exception ex) { WorldStatus.Text = "Thất bại"; Log("Historical World: " + ex); Alert(ex.Message); }
        finally { WorldRenderButton.IsEnabled = true; _worldCts?.Dispose(); _worldCts = null; }
    }

    private void WorldCancelClick(object sender, RoutedEventArgs e) => _worldCts?.Cancel();

    private void WorldOpenMp4Click(object sender, RoutedEventArgs e)
    {
        if (_worldMp4 == null || !File.Exists(_worldMp4)) { Alert("Chưa có MP4 từ lần render này."); return; }
        Process.Start(new ProcessStartInfo(_worldMp4) { UseShellExecute = true });
    }

    private void WorldPlanClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var engine = ResolveWorldEngine();
            var script = WorldScriptText.Text.Trim();
            if (script.Length < 10) throw new ArgumentException("Hãy nhập kịch bản tối thiểu 10 ký tự.");
            var output = WorldOutputPath.Text.Trim();
            if (!Path.IsPathRooted(output)) output = Path.Combine(engine, output);
            Directory.CreateDirectory(output);
            var source = Path.Combine(output, "v6_4_script.txt");
            File.WriteAllText(source, script, Encoding.UTF8);
            var scenePlan = Path.Combine(output, "v6_4_scene_plan_draft.json");
            var draft = new JsonObject
            {
                ["schema"] = "h2.historical_world.scene_plan.v6.4",
                ["status"] = "draft_requires_review",
                ["source_script"] = Path.GetFileName(source),
                ["title"] = script.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Historical World",
                ["shots"] = new JsonArray()
            };
            File.WriteAllText(scenePlan, draft.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            WorldStatus.Text = "Đã lưu Scene Plan nháp — cần bổ sung shots và duyệt";
            Log("Scene Plan nháp: " + scenePlan);
            Alert("Đã tạo Scene Plan nháp, chưa gọi AI và chưa tạo Scene JSON. Không thể render bản nháp.");
        }
        catch (Exception ex) { Log(ex.ToString()); Alert(ex.Message); }
    }

    private async void WorldAiPlanClick(object sender, RoutedEventArgs e)
    {
        if (_worldCts != null) { Alert("Đang có tác vụ Historical World."); return; }
        var key = BuildAiRequest().ApiKey;
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("H2_AI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Alert("Nhập API key tại tab AI Kịch bản hoặc cấu hình H2_AI_API_KEY. Không lưu API key vào dự án.");
            return;
        }
        _worldCts = new CancellationTokenSource();
        try
        {
            WorldSyncAiClick(sender, e);
            var engine = ResolveWorldEngine();
            var script = WorldScriptText.Text.Trim();
            if (script.Length < 10) throw new ArgumentException("Kịch bản phải có ít nhất 10 ký tự.");
            var output = WorldOutputPath.Text.Trim();
            if (!Path.IsPathRooted(output)) output = Path.Combine(engine, output);
            Directory.CreateDirectory(output);
            var source = Path.Combine(output, "v6_4_script.txt");
            var draft = Path.Combine(output, "v6_4_scene_plan_ai_draft.json");
            File.WriteAllText(source, script, Encoding.UTF8);
            var start = new ProcessStartInfo(_settings.PythonExecutable)
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true,
                WorkingDirectory = engine
            };
            foreach (var arg in new[] { Path.Combine(engine, "tools", "v6_4_ai_scene_plan.py"),
                "--script", source, "--output", draft, "--endpoint",
                WorldAiEndpoint.Text.Trim(), "--model", WorldAiModel.Text.Trim() })
                start.ArgumentList.Add(arg);
            start.Environment["H2_AI_API_KEY"] = key;
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được Python.");
            using var registration = _worldCts.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            });
            WorldStatus.Text = "AI đang tạo Scene Plan...";
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var outText = await stdout;
            var errText = await stderr;
            _worldCts.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidOperationException(errText);
            Log(outText);
            WorldStatus.Text = "AI Scene Plan nháp đã tạo — cần duyệt";
            Alert("Đã tạo Scene Plan: " + draft + "\nCần duyệt nội dung lịch sử. Scene Plan chưa tự chuyển thành V6.2 Scene JSON.");
        }
        catch (OperationCanceledException) { WorldStatus.Text = "Đã hủy AI"; }
        catch (Exception ex) { WorldStatus.Text = "AI thất bại"; Log(ex.ToString()); Alert(ex.Message); }
        finally { _worldCts?.Dispose(); _worldCts = null; }
    }

    private void WorldLoadProjectScriptClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) { Alert("Chọn dự án trước."); return; }
        var script = AiScriptEditor.Text.Trim();
        if (string.IsNullOrWhiteSpace(script))
            script = LoadIfExists(AiPath(_selected.Name, "01-SCRIPT", "txt")).Trim();
        if (string.IsNullOrWhiteSpace(script))
            script = AiVoiceEditor.Text.Trim();
        if (string.IsNullOrWhiteSpace(script))
            throw new InvalidOperationException("Dự án chưa có kịch bản 01-SCRIPT hoặc 02-VOICE.");
        WorldScriptText.Text = script;
        WorldStatus.Text = "Đã nạp kịch bản dự án: " + _selected.Name;
    }

    private void WorldSyncAiClick(object sender, RoutedEventArgs e)
    {
        var request = BuildAiRequest();
        var endpoint = request.Provider switch
        {
            AiScriptProvider.Custom => AiEndpoint.Resolve(request.BaseUrl ?? "", request.CustomEndpoint).ToString(),
            AiScriptProvider.OpenAI => "https://api.openai.com/v1/chat/completions",
            _ => throw new NotSupportedException("Gemini native chưa hỗ trợ qua AI Scene Plan CLI; hãy chọn OpenAI hoặc Custom.")
        };
        if (request.Provider == AiScriptProvider.Custom &&
            request.CustomEndpoint == AiCustomEndpoint.Responses)
            throw new NotSupportedException("AI Scene Plan yêu cầu Chat Completions; hãy chọn endpoint tương ứng.");
        WorldAiEndpoint.Text = endpoint;
        WorldAiModel.Text = request.Model;
        WorldStatus.Text = "Đã đồng bộ AI từ tab AI Kịch bản";
    }

    private void WorldBrowseEngineClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Chọn thư mục h2-historical-world" };
        if (dialog.ShowDialog(this) == true)
            WorldEnginePath.Text = dialog.FolderName;
    }

    private void WorldCheckEnvironmentClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var engine = ResolveWorldEngine();
            if (!File.Exists(Path.Combine(engine, "tools", "v6_4_pipeline.py")))
                throw new FileNotFoundException("Thiếu V6.4 pipeline");
            if (!File.Exists(WorldBlenderPath.Text.Trim()))
                throw new FileNotFoundException("Không tìm thấy Blender.exe", WorldBlenderPath.Text.Trim());
            var scene = WorldScenePath.Text.Trim();
            if (!Path.IsPathRooted(scene)) scene = Path.Combine(engine, scene);
            if (!File.Exists(scene)) throw new FileNotFoundException("Không tìm thấy Scene JSON", scene);
            WorldStatus.Text = "Engine / Blender / Scene JSON: OK";
        }
        catch (Exception ex) { WorldStatus.Text = "Môi trường chưa sẵn sàng"; Alert(ex.Message); }
    }

    private async void WorldPreviewClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var engine = ResolveWorldEngine();
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file Three.js preview HTML do engine xuất",
                Filter = "HTML preview (*.html)|*.html"
            };
            dialog.InitialDirectory = Directory.Exists(Path.Combine(engine, "build"))
                ? Path.Combine(engine, "build") : engine;
            if (dialog.ShowDialog(this) != true) return;
            await WorldPreviewBrowser.EnsureCoreWebView2Async();
            WorldPreviewBrowser.CoreWebView2.Navigate(new Uri(dialog.FileName).AbsoluteUri);
            WorldStatus.Text = "Three.js preview: " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex) { WorldStatus.Text = "Preview lỗi"; Alert(ex.Message); }
    }

}
