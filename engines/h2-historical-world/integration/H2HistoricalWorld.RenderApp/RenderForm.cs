using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace H2.HistoricalWorld;

/// <summary>Standalone Windows render console, designed to validate integration before embedding in H2 Studio.</summary>
public sealed class RenderForm : Form
{
    private readonly TextBox scene = new() { Dock = DockStyle.Fill };
    private readonly TextBox output = new() { Dock = DockStyle.Fill };
    private readonly TextBox python = new() { Dock = DockStyle.Fill, Text = "python" };
    private readonly TextBox blender = new() { Dock = DockStyle.Fill, Text = @"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" };
    private readonly NumericUpDown frames = new() { Minimum = 2, Maximum = 100000, Value = 48, Dock = DockStyle.Fill };
    private readonly NumericUpDown fps = new() { Minimum = 1, Maximum = 120, Value = 24, Dock = DockStyle.Fill };
    private readonly Button run = new() { Text = "Render MP4", AutoSize = true };
    private readonly Button cancel = new() { Text = "Cancel", AutoSize = true, Enabled = false };
    private readonly Button open = new() { Text = "Open MP4", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Text = "Ready", AutoSize = true };
    private readonly TextBox log = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Dock = DockStyle.Fill };
    private CancellationTokenSource? cts;
    private string? lastMp4;
    private readonly string pipeline;

    public RenderForm()
    {
        Text = "H2 Historical World V6.4 — Render";
        Width = 940; Height = 620;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        pipeline = Path.Combine(root, "tools", "v6_4_pipeline.py");
        scene.Text = Path.Combine(root, "build", "v6_2_scene.json");
        output.Text = Path.Combine(root, "build", "v6_4_ui_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(int row, string name, Control control)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.Controls.Add(new Label { Text = name, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
        Add(0, "Scene JSON", scene);
        Add(1, "Output folder", output);
        Add(2, "Python", python);
        Add(3, "Blender", blender);
        Add(4, "Frames", frames);
        Add(5, "FPS", fps);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        actions.Controls.AddRange(new Control[] { run, cancel, open, status });
        Add(6, "Actions", actions);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Pipeline log", Dock = DockStyle.Fill }, 0, 7);
        layout.Controls.Add(log, 1, 7);
        Controls.Add(layout);
        run.Click += async (_, _) => await RenderAsync();
        cancel.Click += (_, _) => cts?.Cancel();
        open.Click += (_, _) =>
        {
            if (lastMp4 != null && File.Exists(lastMp4))
                Process.Start(new ProcessStartInfo(lastMp4) { UseShellExecute = true });
        };
    }

    private void Append(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(Append), message); return; }
        log.AppendText(message + Environment.NewLine);
    }

    private void SetStatus(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(SetStatus), message); return; }
        status.Text = message;
    }

    private async Task RenderAsync()
    {
        run.Enabled = false; cancel.Enabled = true; open.Enabled = false;
        lastMp4 = null;
        cts = new CancellationTokenSource();
        var service = new HistoricalWorldRenderService();
        service.Progress += (stage, state) => SetStatus(stage + ": " + state);
        service.Log += Append;
        try
        {
            lastMp4 = await service.RenderAsync(python.Text.Trim(), pipeline,
                Path.GetFullPath(scene.Text.Trim()), Path.GetFullPath(output.Text.Trim()),
                blender.Text.Trim(), (int)frames.Value, (int)fps.Value, cts.Token);
            SetStatus("Completed");
            Append("MP4: " + lastMp4);
            open.Enabled = true;
        }
        catch (OperationCanceledException) { SetStatus("Cancelled"); Append("Render cancelled."); }
        catch (Exception ex) { SetStatus("Failed"); Append("ERROR: " + ex.Message); }
        finally { cancel.Enabled = false; run.Enabled = true; cts.Dispose(); cts = null; }
    }
}
