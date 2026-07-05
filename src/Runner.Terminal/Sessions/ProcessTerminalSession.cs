using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AntigravityTaskRunner.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pty.Net;

namespace AntigravityTaskRunner.Terminal.Sessions;

public class ProcessTerminalSession : ITerminalSession
{
    private readonly TerminalOptions _options;
    private readonly ILogger<ProcessTerminalSession> _logger;
    private IPtyConnection? _pty;
    private readonly StringBuilder _stdOutBuffer = new();
    private readonly object _outputLock = new();
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    // Completed when the underlying process actually exits (driven by the PTY ProcessExited
    // lifecycle event), so teardown can wait on a real exit signal instead of polling.
    private readonly TaskCompletionSource _exitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _processExited;

    private bool _disposed;
    private CancellationTokenSource? _readTcs;
    private Task? _readTask;

    // Optional per-task spawn override (one-shot mode). When set, the session launches this
    // executable/args directly instead of the configured interactive shell.
    private string? _spawnApp;
    private IReadOnlyList<string>? _spawnCommandLine;
    private string? _spawnCwd;

    // Upper bound on how long teardown will wait for a killed process tree to be reaped.
    // This is a safety net against a hang; the normal path is driven by the real exit event.
    private static readonly TimeSpan ProcessExitGrace = TimeSpan.FromSeconds(10);

    public ProcessTerminalSession(IOptions<RunnerOptions> options, ILogger<ProcessTerminalSession> logger)
    {
        _options = options.Value.Terminal;
        _logger = logger;
    }

    public void ConfigureSpawn(string app, IReadOnlyList<string> commandLine, string? workingDirectory)
    {
        _spawnApp = app;
        _spawnCommandLine = commandLine;
        _spawnCwd = workingDirectory;
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        // Use the per-task spawn override (one-shot mode) if configured; otherwise launch the
        // configured interactive shell.
        var app = !string.IsNullOrWhiteSpace(_spawnApp) ? _spawnApp! : _options.ShellPath;

        string[] argsArray;
        if (_spawnCommandLine != null)
        {
            argsArray = _spawnCommandLine.ToArray();
        }
        else
        {
            argsArray = string.IsNullOrWhiteSpace(_options.Arguments)
                ? Array.Empty<string>()
                : new[] { _options.Arguments };
        }

        var cwd = !string.IsNullOrWhiteSpace(_spawnCwd) ? _spawnCwd! : Environment.CurrentDirectory;

        _logger.LogInformation("Starting terminal session: {App} {Arguments}", app, string.Join(' ', argsArray));

        var ptyOptions = new PtyOptions
        {
            App = app,
            CommandLine = argsArray,
            Cwd = cwd,
            Environment = _options.EnvironmentVariables.ToDictionary(k => k.Key, v => v.Value),
            ForceWinPty = true,
            Cols = 120,
            Rows = 30
        };

        var pty = await PtyProvider.SpawnAsync(ptyOptions, token);
        _pty = pty;
        _stopwatch.Start();

        // Observe the process lifecycle directly. When the root shell (and therefore the PTY)
        // exits, complete the exit signal so teardown does not rely on arbitrary delays.
        pty.ProcessExited += (_, _) =>
        {
            _processExited = true;
            _exitTcs.TrySetResult();
        };

        // Guard against the race where the process exits before the handler was attached.
        if (pty.WaitForExit(0))
        {
            _processExited = true;
            _exitTcs.TrySetResult();
        }

        _readTcs = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadOutputAsync(_readTcs.Token), _readTcs.Token);
    }

    private async Task ReadOutputAsync(CancellationToken token)
    {
        var buffer = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested && _pty != null)
            {
                var bytesRead = await _pty.ReaderStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (bytesRead == 0)
                {
                    break; // stream closed
                }

                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                lock (_outputLock)
                {
                    _stdOutBuffer.Append(text);
                }

                // Echo the PTY output to the host console so the user can see the interaction
                Console.Write(text);

                _logger.LogTrace("PTY OUT: {Data}", text);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the session is being torn down.
        }
        catch (ObjectDisposedException)
        {
            // Expected when the reader stream is disposed during teardown.
        }
        catch (IOException)
        {
            // Expected when the pseudo-console handle is closed underneath the read.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from PTY stream.");
        }
    }

    public async Task SendInputAsync(string input, CancellationToken token = default)
    {
        EnsureProcessStarted();
        _logger.LogTrace("Sending input to terminal: {Input}", input);

        var inputBytes = Encoding.UTF8.GetBytes(input + Environment.NewLine);
        await _pty!.WriterStream.WriteAsync(inputBytes.AsMemory(0, inputBytes.Length), token);
        await _pty.WriterStream.FlushAsync(token);
    }

    public (string StdOut, string StdErr) GetCurrentOutput()
    {
        lock (_outputLock)
        {
            return (_stdOutBuffer.ToString(), string.Empty);
        }
    }

    public void ClearOutputBuffers()
    {
        lock (_outputLock)
        {
            _stdOutBuffer.Clear();
        }
    }

    public async Task<TerminalSessionResult> WaitForExitAsync(CancellationToken token = default)
    {
        EnsureProcessStarted();

        // Wait for the genuine process exit. The caller's token bounds the wait (e.g. the task
        // timeout in one-shot mode); cancellation surfaces as OperationCanceledException so the
        // caller can treat it as a timeout. This is the authoritative completion signal.
        if (!_processExited)
        {
            await _exitTcs.Task.WaitAsync(token);
        }

        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
        }

        var (stdOut, stdErr) = GetCurrentOutput();
        int exitCode = _processExited ? _pty!.ExitCode : -1;

        return new TerminalSessionResult(exitCode, stdOut, stdErr, _stopwatch.Elapsed);
    }

    public async Task KillAsync(CancellationToken token = default)
    {
        var pty = _pty;
        if (pty == null)
        {
            return;
        }

        _logger.LogInformation("Terminating terminal session process tree (PID {Pid}).", pty.Pid);

        // 1. Kill the ENTIRE process tree. pty.Kill() only terminates the root shell (cmd.exe);
        //    the Antigravity CLI (agy) and any tools it spawned run as separate child processes
        //    and would otherwise survive teardown and keep running into the next task, producing
        //    overlapping sessions. taskkill /T terminates the whole tree.
        await KillProcessTreeAsync(pty.Pid, token);

        // 2. Also close the pseudo-console / root process directly as a fallback.
        try
        {
            pty.Kill();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PTY Kill() failed (process may already have exited).");
        }

        // 3. Wait for the OS to actually reap the process so the next session cannot overlap.
        await WaitForProcessReapAsync(token);
    }

    /// <summary>
    /// After a kill, waits for the OS to reap the process using the lifecycle event, bounded by a
    /// short grace period so teardown can never deadlock even if the exit notification is lost.
    /// Never throws — teardown must always run to completion.
    /// </summary>
    private async Task WaitForProcessReapAsync(CancellationToken token)
    {
        var pty = _pty;
        if (pty == null || _processExited)
        {
            return;
        }

        try
        {
            await _exitTcs.Task.WaitAsync(ProcessExitGrace, token);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timed out waiting for terminal process (PID {Pid}) to exit after kill.", pty.Pid);
        }
        catch (OperationCanceledException)
        {
            // The bounded teardown wait was cancelled; the kill has already been issued.
        }
    }

    /// <summary>
    /// Forcefully terminates the entire process tree rooted at <paramref name="pid"/>.
    /// On Windows this uses <c>taskkill /T /F</c> because killing the PTY root shell alone
    /// leaves child processes (the agy CLI and its subprocesses) orphaned and still running.
    /// </summary>
    private async Task KillProcessTreeAsync(int pid, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var proc = System.Diagnostics.Process.Start(CreateTaskKillStartInfo(pid));
            if (proc != null)
            {
                await proc.WaitForExitAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // Bounded teardown was cancelled; the PTY-level kill below still runs.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "taskkill failed to terminate process tree for PID {Pid}.", pid);
        }
    }

    private void KillProcessTreeSync(int pid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var proc = System.Diagnostics.Process.Start(CreateTaskKillStartInfo(pid));
            proc?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "taskkill (sync) failed to terminate process tree for PID {Pid}.", pid);
        }
    }

    private static System.Diagnostics.ProcessStartInfo CreateTaskKillStartInfo(int pid) =>
        new("taskkill", $"/PID {pid} /T /F")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

    private void EnsureProcessStarted()
    {
        if (_pty == null)
        {
            throw new InvalidOperationException("Terminal session has not been started.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Stop the background reader first so it releases the stream.
        if (_readTcs != null)
        {
            try
            {
                _readTcs.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }
        }

        var pty = _pty;
        if (pty != null)
        {
            try
            {
                // Dispose is the last-resort safety net. The pipeline normally tears the session
                // down via KillAsync first; if that did not happen, ensure the whole process tree
                // is terminated synchronously so no agy process leaks into the next task.
                if (!_processExited)
                {
                    KillProcessTreeSync(pty.Pid);
                    pty.Kill();
                    pty.WaitForExit(5000);
                }

                pty.ReaderStream?.Dispose();
                pty.WriterStream?.Dispose();
            }
            catch
            {
                // Ignore errors during disposal.
            }
        }

        // Give the background reader a bounded moment to observe cancellation and unwind.
        try
        {
            _readTask?.Wait(1000);
        }
        catch (AggregateException)
        {
            // Expected: the reader completes via OperationCanceledException on teardown.
        }

        _readTcs?.Dispose();
        GC.SuppressFinalize(this);
    }
}
