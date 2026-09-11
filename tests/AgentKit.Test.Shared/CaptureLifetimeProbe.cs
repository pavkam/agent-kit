// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Counts owned cleanup and optionally exposes a controlled asynchronous completion or failure.</summary>
/// <param name="dispose">Optional callback run on every disposal, outside any probe synchronization.</param>
public sealed class CaptureLifetimeProbe(Func<ValueTask>? dispose = null): IAsyncDisposable
{
    private int _calls;

    /// <summary>Gets the thread-safe number of cleanup attempts.</summary>
    /// <value>The count includes pending and failed attempts.</value>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Counts one attempt and delegates to the configured completion.</summary>
    /// <returns>The callback's completion and failure, or synchronous success when no callback was supplied.</returns>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _calls);
        return dispose?.Invoke() ?? ValueTask.CompletedTask;
    }
}
