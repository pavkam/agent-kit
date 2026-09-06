// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Receives immutable session lifecycle events after they have already
/// occurred.
/// </summary>
/// <remarks>
/// A sink is additive and observation-only: it cannot mutate the operation
/// that produced the event, veto it, or influence the coordinator's
/// decision. Registration order determines delivery order across the
/// additive set of configured sinks.
/// </remarks>
public interface ISessionEventSink
{
    /// <summary>Delivers one session event.</summary>
    /// <param name="sessionEvent">The event to deliver.</param>
    /// <param name="cancellationToken">A token used to cancel delivery.</param>
    /// <returns>A task that completes once the event has been handled.</returns>
    public ValueTask PublishAsync(
        SessionEvent sessionEvent,
        CancellationToken cancellationToken = default);
}
