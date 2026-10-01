// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Lets a <see cref="IRunEventSink"/> that buffers deliveries drain them before engine shutdown completes.</summary>
/// <remarks>
/// A <see cref="RunEventDelivery.Required"/> sink's <see cref="IRunEventSink.PublishAsync"/> is awaited inline by the
/// publisher, so a sink that accepts an event only into an internal bounded queue completes delivery later. Such a sink
/// implements this interface so <see cref="IRequiredRunEventSinkCoordinator"/> can wait for the queue to empty within the
/// deadline declared by its <see cref="RunEventSinkRegistration"/>. A sink without buffering needs no implementation: its
/// inline acceptance already is its drain. Flushing cannot rewrite an accepted event as persisted; cancellation only stops
/// the wait.
/// </remarks>
public interface IFlushableRunEventSink: IRunEventSink
{
    /// <summary>Waits until every event this sink already accepted has been delivered or has failed terminally.</summary>
    /// <param name="cancellationToken">Cancels the wait, including when the registration's flush deadline elapses.</param>
    /// <returns>Completion once the sink's buffered deliveries are drained.</returns>
    /// <exception cref="OperationCanceledException">The wait was cancelled before the sink drained.</exception>
    public ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
