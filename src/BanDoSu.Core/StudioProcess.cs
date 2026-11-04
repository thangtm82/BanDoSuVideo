using System.Diagnostics;
using System.Text;

namespace BanDoSu.Core;

public sealed record ProcessResult(int ExitCode, string Output);

public sealed class StudioProcess
{
    public async Task<ProcessResult> RunAsync(
        string executable, IEnumerable<string> arguments, string workingDirectory,
        Action<string>? onLine = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workingDirectory))
            throw new DirectoryNotFoundException(workingDirectory);
        var psi = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var gate = new object();
        void Append(string? text)
        {
            if (text is null) return;
            lock (gate) output.AppendLine(text);
            onLine?.Invoke(text);
        }
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        if (!process.Start()) throw new InvalidOperationException($"Could not start {executable}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var reg = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        process.WaitForExit(); // flush asynchronous DataReceived handlers
        return new ProcessResult(process.ExitCode, output.ToString());
    }
}
