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
    private bool _disposed;
    private CancellationTokenSource? _readTcs;
    private Task? _readTask;

    public ProcessTerminalSession(IOptions<RunnerOptions> options, ILogger<ProcessTerminalSession> logger)
    {
        _options = options.Value.Terminal;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        _logger.LogInformation("Starting terminal session: {ShellPath} {Arguments}", _options.ShellPath, _options.Arguments);

        var argsArray = string.IsNullOrWhiteSpace(_options.Arguments) 
            ? Array.Empty<string>() 
            : new[] { _options.Arguments };

        var ptyOptions = new PtyOptions
        {
            App = _options.ShellPath,
            CommandLine = argsArray,
            Cwd = Environment.CurrentDirectory,
            Environment = _options.EnvironmentVariables.ToDictionary(k => k.Key, v => v.Value),
            ForceWinPty = true,
            Cols = 120,
            Rows = 30
        };

        _pty = await PtyProvider.SpawnAsync(ptyOptions, token);
        _stopwatch.Start();

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
            // Expected
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

        try
        {
            // Simple wait loop since IPtyConnection doesn't have WaitForExitAsync
            while (!token.IsCancellationRequested && _pty!.WaitForExit(100) == false)
            {
                await Task.Delay(100, token);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Wait for exit cancelled.");
            await KillAsync(CancellationToken.None);
            throw;
        }

        _stopwatch.Stop();
        
        // Wait briefly for output buffers to flush
        await Task.Delay(100, CancellationToken.None);

        var (stdOut, stdErr) = GetCurrentOutput();

        return new TerminalSessionResult(
            _pty!.ExitCode,
            stdOut,
            stdErr,
            _stopwatch.Elapsed
        );
    }

    public Task KillAsync(CancellationToken token = default)
    {
        if (_pty != null)
        {
            _logger.LogInformation("Killing terminal session process.");
            try
            {
                _pty.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill PTY process.");
            }
        }
        
        return Task.CompletedTask;
    }

    private void EnsureProcessStarted()
    {
        if (_pty == null)
        {
            throw new InvalidOperationException("Terminal session has not been started.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        if (_readTcs != null)
        {
            _readTcs.Cancel();
            _readTcs.Dispose();
        }

        if (_pty != null)
        {
            try
            {
                _pty.Kill();
                _pty.ReaderStream?.Dispose();
                _pty.WriterStream?.Dispose();
            }
            catch
            {
                // Ignore during disposal
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
