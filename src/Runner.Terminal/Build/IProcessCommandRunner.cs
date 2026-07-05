using System.Diagnostics;
using System.Text;

namespace AntigravityTaskRunner.Terminal.Build;

/// <summary>Result of a single external command execution.</summary>
public sealed record CommandResult(int ExitCode, string Output, bool TimedOut);

/// <summary>
/// Thin abstraction over external process execution so build validation is unit-testable.
/// </summary>
public interface IProcessCommandRunner
{
    Task<CommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken token = default);
}

/// <summary>Executes commands with <see cref="Process"/>, capturing combined output.</summary>
public sealed class SystemProcessCommandRunner : IProcessCommandRunner
{
    public async Task<CommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var outputLock = new object();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (outputLock) output.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (outputLock) output.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already exited */ }

            token.ThrowIfCancellationRequested(); // caller cancellation propagates
            string timeoutOutput;
            lock (outputLock) timeoutOutput = output.ToString();
            return new CommandResult(-1, timeoutOutput, TimedOut: true);
        }

        string finalOutput;
        lock (outputLock) finalOutput = output.ToString();
        return new CommandResult(process.ExitCode, finalOutput, TimedOut: false);
    }
}
