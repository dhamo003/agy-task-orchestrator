using System;
using System.Threading;

namespace AntigravityTaskRunner.Core.Cancellation;

/// <summary>
/// Manages cancellation requests, such as from Ctrl+C.
/// </summary>
public interface ICancellationManager
{
    /// <summary>
    /// Gets a token that is canceled when cancellation is requested.
    /// </summary>
    CancellationToken Token { get; }

    /// <summary>
    /// Requests cancellation.
    /// </summary>
    void RequestCancellation();
}
