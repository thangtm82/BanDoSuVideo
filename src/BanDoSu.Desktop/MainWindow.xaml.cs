using BanDoSu.Core;
using Microsoft.Win32;
using System.ComponentModel;
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
    private ProjectFiles _projects;
    private StudioCommands _commands;
    private ProjectSummary? _selected;
    private ShotDocument? _shots;
    private string? _scriptPath;
    private CancellationTokenSource? _operation;
    private Process? _previewHost;
    private string? _previewUrl;

    public MainWindow()
    {
        InitializeComponent();
        MapBrowser.NavigationCompleted += async (_, _) =>
        {
            try
            {
                if (MapBrowser.CoreWebView2 == null) return;
                await MapBrowser.ExecuteScriptAsync(
                    "(async () => { for (let i = 0; i < 150 && !window.__ready && !window.__bootError; i++) await new Promise(r => setTimeout(r, 100)); if (window.__ready) await window.renderFrame(0); })()");
                var bootError = await MapBrowser.ExecuteScriptAsync("window.__bootError || null");
                if (bootError != "null") Log("Map boot error: " + bootError);
            }
            catch (Exception ex) { Log("Live map error: " + ex.Message); }
        };
        _projects = new ProjectFiles(_settings);
        _commands = new StudioCommands(_settings);
        FillSettings();
        RefreshProjects();
    }

    private void FillSettings()
    {
        StudioRootText.Text = _settings.StudioRoot;
        NodeText.Text = _settings.NodeExecutable;
        PythonText.Text = _settings.PythonExecutable;
        FfmpegText.Text = _settings.FfmpegExecutable;
    }

    private void Log(string text) => Dispatcher.InvokeAsync(() =>
    {
        LogOutput.AppendText(text + Environment.NewLine);
        LogOutput.ScrollToEnd();
    });

    private void Alert(string message) => MessageBox.Show(this, message, "BanDoSu Studio",
        MessageBoxButton.OK, MessageBoxImage.Information);

    private bool HasStudio()
    {
        if (File.Exists(Path.Combine(_settings.StudioRoot, "render.mjs"))) return true;
        Alert("Please select the installed H2 Studio root in Settings (folder with render.mjs).");
        MainTabs.SelectedIndex = 5;
        return false;
    }

    private bool HasProject()
    {
        if (!HasStudio()) return false;
        if (_selected is not null) return true;
        Alert("Select a project in the Projects tab first.");
        MainTabs.SelectedIndex = 0;
        return false;
    }

    private async Task ExecuteAsync(Func<Action<string>, CancellationToken, Task<ProcessResult>> work, string label,
        Action? onSuccess = null)
    {
        if (_operation != null) { Alert("Another operation is running. Cancel or finish it first."); return; }
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation;
        RenderStatus.Text = label + " running...";
        Log("=== " + label + " ===");
        try
        {
            var result = await work(Log, cancellation.Token);
            if (result.ExitCode != 0)
            {
                RenderStatus.Text = label + " failed";
                Log($"FAIL: exit code {result.ExitCode}");
                Alert(label + " failed; inspect the log.");
                return;
            }
            RenderStatus.Text = label + " complete";
            Log("DONE: " + label);
            onSuccess?.Invoke();
        }
        catch (OperationCanceledException)
        {
            RenderStatus.Text = "Cancelled";
            Log("CANCELLED: " + label);
        }
        catch (Exception ex)
        {
            RenderStatus.Text = label + " failed";
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

    private void RefreshProjectsClick(object sender, RoutedEventArgs e) => RefreshProjects();

    private async void CreateProjectClick(object sender, RoutedEventArgs e)
    {
        if (!HasStudio()) return;
        var slug = NewSlug.Text.Trim();
        if (!Regex.IsMatch(slug, @"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,80}$"))
        { Alert("Project name can use letters, digits, hyphen and underscore."); return; }
        if (Directory.Exists(_projects.ResolveProject(slug)))
        { Alert("Project already exists. Choose a new name."); return; }
        await ExecuteAsync((log, ct) => _commands.CreateProjectAsync(slug, NewTitle.Text.Trim(),
            NewCaptions.IsChecked == true, log, ct), "Create project", () =>
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
        ProjectHeader.Text = _selected?.Title ?? "No project selected";
        if (_selected == null) return;
        var projectScript = _projects.FileInProject(_selected.Name, "input", "02-VOICE.txt");
        _scriptPath = projectScript;
        ScriptEditor.Text = File.Exists(projectScript) ? File.ReadAllText(projectScript, Encoding.UTF8) : "";
        VoiceFolder.Text = _projects.FileInProject(_selected.Name, "input", "voice");
        try { LoadShots(); } catch (Exception ex) { Log("Scene load: " + ex.Message); }
        StopPreviewHost();
    }

    private void OpenProjectFolderClick(object sender, RoutedEventArgs e)
    { if (HasProject()) OpenPath(_selected!.Directory); }

    private void LoadScriptClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Text (*.txt)|*.txt|All files|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        _scriptPath = dlg.FileName;
        ScriptEditor.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
        Log("Loaded script: " + _scriptPath);
    }

    private bool SaveScript()
    {
        if (!HasProject()) return false;
        // Always make the project's copy canonical, even if the source was external.
        var canonical = _projects.FileInProject(_selected!.Name, "input", "02-VOICE.txt");
        ProjectFiles.SaveText(canonical, ScriptEditor.Text);
        _scriptPath = canonical;
        Log("Saved voice script: " + canonical);
        return true;
    }

    private void SaveScriptClick(object sender, RoutedEventArgs e) => SaveScript();

    private void BrowseVoiceClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose folder containing 1.mp3, 2.mp3, ..." };
        if (dlg.ShowDialog(this) == true) VoiceFolder.Text = dlg.FolderName;
    }

    private bool ValidateVoice()
    {
        if (!Directory.Exists(VoiceFolder.Text)) { Alert("MP3 folder does not exist."); return false; }
        var segments = ScriptEditor.Text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (segments.Length == 0) { Alert("Voice script is empty."); return false; }
        var missing = Enumerable.Range(1, segments.Length).Where(i =>
            !File.Exists(Path.Combine(VoiceFolder.Text, i + ".mp3"))).ToArray();
        if (missing.Length > 0)
        { Alert("Missing numbered MP3 files: " + string.Join(", ", missing.Take(20))); return false; }
        Log($"Voice check OK: {segments.Length} segments and numbered MP3s.");
        return true;
    }

    private void ValidateVoiceClick(object sender, RoutedEventArgs e) => ValidateVoice();

    private async void PrepareVoiceClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject() || !ValidateVoice() || !SaveScript()) return;
        await ExecuteAsync((log, ct) => _commands.PrepareVoiceAsync(_selected!.Name,
            VoiceFolder.Text, _scriptPath!, log, ct), "Prepare voice");
    }

    private async void DraftShotsClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        var timeline = _projects.FileInProject(_selected!.Name, "timeline", "lines.json");
        if (!File.Exists(timeline)) { Alert("Prepare voice first: timeline/lines.json missing."); return; }
        var shots = _projects.FileInProject(_selected.Name, "story", "shots.json");
        if (File.Exists(shots) && MessageBox.Show(this,
                "Draft generation may replace existing scene edits. Continue?",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (File.Exists(shots)) File.Copy(shots, shots + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
        await ExecuteAsync((log, ct) => _commands.DraftShotsAsync(_selected.Name, log, ct),
            "Generate draft scenes", () =>
            {
                LoadShots();
                if (_shots is not null && _selected is not null)
                {
                    _shots.SetInitialMapDefaults();
                    ProjectFiles.SaveJson(
                        _projects.FileInProject(_selected.Name, "story", "shots.json"),
                        _shots.Root);
                    ShotsGrid.ItemsSource = _shots.Rows;
                    Log("Initialized map templates and first camera view.");
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
        Log("Loaded scenes: " + _shots.Shots.Count);
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
        var shot = _shots.Rows[ShotsGrid.SelectedIndex];
        MapTime.Value = Math.Clamp(shot.Start, MapTime.Minimum, MapTime.Maximum);
    }

    private void ApplyShotClick(object sender, RoutedEventArgs e)
    {
        if (_shots == null || ShotsGrid.SelectedIndex < 0) { Alert("Select a scene first."); return; }
        try
        {
            _shots.UpdateArgs(ShotsGrid.SelectedIndex, ArgsEditor.Text);
            Log("Applied args to scene " + _shots.Rows[ShotsGrid.SelectedIndex].Id);
        }
        catch (Exception ex) { Alert("Invalid scene JSON: " + ex.Message); }
    }

    private void SaveShotsClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject() || _shots == null) return;
        try
        {
            _shots.Validate();
            var file = _projects.FileInProject(_selected!.Name, "story", "shots.json");
            if (File.Exists(file)) File.Copy(file, file + ".bak", true);
            ProjectFiles.SaveJson(file, _shots.Root);
            Log("Saved scenes: " + file);
        }
        catch (Exception ex) { Alert("Cannot save scenes: " + ex.Message); }
    }

    private async void StartLiveClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        if (_previewHost != null && !_previewHost.HasExited) { Alert("Live preview already running."); return; }
        try
        {
            var script = Path.Combine(AppContext.BaseDirectory, "integration", "preview-host.mjs");
            var psi = new ProcessStartInfo(_settings.NodeExecutable)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = _settings.StudioRoot
            };
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add("--studio");
            psi.ArgumentList.Add(_settings.StudioRoot);
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(_selected!.Name);
            _previewHost = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _previewHost.OutputDataReceived += (_, args) =>
            {
                if (args.Data?.StartsWith("PREVIEW_URL ", StringComparison.Ordinal) == true)
                    ready.TrySetResult(args.Data["PREVIEW_URL ".Length..]);
                else if (args.Data != null) Log(args.Data);
            };
            _previewHost.ErrorDataReceived += (_, args) => { if (args.Data != null) Log("[preview] " + args.Data); };
            _previewHost.Exited += (_, _) => ready.TrySetException(new InvalidOperationException("Preview server exited before startup."));
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
            LiveStatus.Text = "Running — " + _selected.Name;
            Log("Live map: " + _previewUrl);
        }
        catch (Exception ex) { StopPreviewHost(); Alert("Cannot start live map: " + ex.Message); }
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
        catch (Exception ex) { Log("Seek error: " + ex.Message); }
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
        finally { _previewHost?.Dispose(); _previewHost = null; _previewUrl = null; LiveStatus.Text = "Stopped"; }
    }

    private async Task Render(RenderMode mode, string label, Action? success = null)
    {
        if (!HasProject()) return;
        await ExecuteAsync((log, ct) => _commands.RenderAsync(_selected!.Name, mode, log, ct), label, success);
    }

    private async void CheckRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Check, "Scene check");
    private async void SheetRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Sheet, "Contact sheet", () =>
        OpenPath(Path.Combine(_selected!.Directory, "build", "sheets")));
    private async void PreviewRenderClick(object sender, RoutedEventArgs e) =>
        await Render(RenderMode.Preview, "30-second preview", () =>
        {
            var p = Path.Combine(_selected!.Directory, "build", "preview.mp4");
            if (File.Exists(p)) { VideoPlayer.Source = new Uri(p); VideoPlayer.Play(); }
        });
    private async void FullRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Full, "Full video render");
    private async void ThumbnailRenderClick(object sender, RoutedEventArgs e) => await Render(RenderMode.Thumbnail,
        "Thumbnail", () => OpenPath(Path.Combine(_selected!.Directory, "out")));

    private async void MixRenderClick(object sender, RoutedEventArgs e)
    {
        if (!HasProject()) return;
        var video = Path.Combine(_selected!.Directory, "build", "video_silent.mp4");
        if (!File.Exists(video)) { Alert("Full render must finish before mixing audio."); return; }
        await ExecuteAsync((log, ct) => _commands.MixAsync(_selected.Name, log, ct), "Audio mix", () =>
            OpenPath(Path.Combine(_selected.Directory, "out")));
    }

    private void PlayVideoClick(object sender, RoutedEventArgs e) => VideoPlayer.Play();
    private void PauseVideoClick(object sender, RoutedEventArgs e) => VideoPlayer.Pause();
    private void VideoPlayerFailed(object sender, ExceptionRoutedEventArgs e) => Log("Video playback: " + e.ErrorException.Message);
    private void OpenOutputClick(object sender, RoutedEventArgs e)
    { if (HasProject()) OpenPath(Path.Combine(_selected!.Directory, "out")); }

    private static void OpenPath(string path)
    {
        if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void BrowseStudioClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select H2 Studio folder (render.mjs)" };
        if (dlg.ShowDialog(this) == true) StudioRootText.Text = dlg.FolderName;
    }

    private void SaveSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(Path.Combine(StudioRootText.Text.Trim(), "render.mjs")))
        { Alert("render.mjs was not found in this folder."); return; }
        StopPreviewHost();
        _settings = new StudioSettings
        {
            StudioRoot = StudioRootText.Text.Trim(),
            NodeExecutable = NodeText.Text.Trim(),
            PythonExecutable = PythonText.Text.Trim(),
            FfmpegExecutable = FfmpegText.Text.Trim()
        };
        _settings.Save();
        _projects = new ProjectFiles(_settings);
        _commands = new StudioCommands(_settings);
        _selected = null;
        RefreshProjects();
        Log("Settings saved to " + StudioSettings.SettingsPath);
    }

    private async void DoctorClick(object sender, RoutedEventArgs e)
    {
        if (!HasStudio()) return;
        await ExecuteAsync((log, ct) => _commands.DoctorAsync(log, ct), "Studio doctor");
    }

    private void CancelClick(object sender, RoutedEventArgs e) => _operation?.Cancel();

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        _operation?.Cancel();
        StopPreviewHost();
        VideoPlayer.Stop();
    }
}
