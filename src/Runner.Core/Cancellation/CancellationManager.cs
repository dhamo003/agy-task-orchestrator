using System;
using System.Threading;

namespace AntigravityTaskRunner.Core.Cancellation;

/// <summary>
/// Implements cancellation management, handling process exit and Console.CancelKeyPress (Ctrl+C).
/// </summary>
public class CancellationManager : ICancellationManager, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationManager()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public CancellationToken Token => _cts.Token;

    public void RequestCancellation()
    {
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination
        Console.WriteLine("\n[CancellationManager] Ctrl+C detected. Requesting graceful shutdown...");
        RequestCancellation();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        RequestCancellation();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
