// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes committed goal and delegation events.</summary>
/// <remarks>A sink is observational only: it cannot veto, reorder, or mutate a change. A singleton sink must be thread-safe.</remarks>
public interface IGoalEventSink
{
    /// <summary>Delivers one immutable event.</summary>
    /// <param name="goalEvent">The committed event.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task completed when the sink has handled the event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="goalEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default);
}
