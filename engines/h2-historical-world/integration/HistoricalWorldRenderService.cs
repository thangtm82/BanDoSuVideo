using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace H2.HistoricalWorld;

/// <summary>UI-agnostic bridge: MAUI/WinForms can bind Progress events and cancel.</summary>
public sealed class HistoricalWorldRenderService
{
    public event Action<string, string>? Progress;
    public event Action<string>? Log;

    public async Task<string> RenderAsync(
        string pythonExe, string pipelineScript, string sceneJson, string outputDirectory,
        string blenderExe, int frames = 240, int fps = 24,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pipelineScript)) throw new FileNotFoundException("Pipeline script", pipelineScript);
        if (!File.Exists(sceneJson)) throw new FileNotFoundException("Scene JSON", sceneJson);
        if (frames < 2 || fps < 1) throw new ArgumentOutOfRangeException(nameof(frames));
        var start = new ProcessStartInfo(pythonExe)
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(pipelineScript))!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { pipelineScript, "--scene", sceneJson, "--output", outputDirectory,
                                    "--blender", blenderExe, "--frames", frames.ToString(),
                                    "--fps", fps.ToString() })
            start.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (e.Data.StartsWith("H2_PROGRESS ", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(e.Data.Substring("H2_PROGRESS ".Length));
                    var root = doc.RootElement;
                    Progress?.Invoke(root.GetProperty("stage").GetString() ?? "",
                                     root.GetProperty("status").GetString() ?? "");
                }
                catch (JsonException) { Log?.Invoke(e.Data); }
            }
            else Log?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
        if (!process.Start()) throw new InvalidOperationException("Unable to start Python pipeline");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new InvalidOperationException(
            $"Historical World render failed (exit {process.ExitCode}); inspect pipeline log.");
        var mp4 = Path.Combine(Path.GetFullPath(outputDirectory), "v6_4_final.mp4");
        if (!File.Exists(mp4) || new FileInfo(mp4).Length == 0)
            throw new FileNotFoundException("MP4 not created", mp4);
        return mp4;
    }
}
