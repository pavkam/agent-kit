// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Delivers committed events to the sinks a captured profile and registration selected.</summary>
/// <remarks>Singleton coordinators capture the dispatcher, never sink instances. The dispatcher activates each sink inside the current operation scope according to its declared lifetime and disposes that activation after delivery.</remarks>
public interface IGoalEventDispatcher
{
    /// <summary>Delivers one event to every registered sink.</summary>
    /// <param name="goalEvent">The committed event.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task completed when every required sink has handled the event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="goalEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask PublishAsync(GoalEvent goalEvent, CancellationToken cancellationToken = default);
}
