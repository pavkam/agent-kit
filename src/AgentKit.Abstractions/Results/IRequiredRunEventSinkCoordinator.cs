// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Drains required run-event sinks during engine shutdown, each within its own registered flush deadline.</summary>
/// <remarks>
/// Every required sink is flushed concurrently; a sink that implements <see cref="IFlushableRunEventSink"/> is awaited for
/// at most <see cref="RunEventSinkRegistration.FlushDeadline"/>, while a sink without buffering already completed each
/// delivery inline and counts as drained. A timed-out or faulted sink is reported, never thrown, so shutdown cannot deadlock.
/// </remarks>
public interface IRequiredRunEventSinkCoordinator
{
    /// <summary>Waits for every required sink to finish bounded shutdown work.</summary>
    /// <param name="cancellationToken">Cancels the shutdown wait; accepted events are not rewritten as persisted.</param>
    /// <returns>Evidence naming which required sinks drained, timed out, or failed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<RequiredRunEventSinkDrainResult> DrainAsync(CancellationToken cancellationToken = default);
}
