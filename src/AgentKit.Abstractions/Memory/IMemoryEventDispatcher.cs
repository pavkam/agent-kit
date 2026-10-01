// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Delivers immutable memory events to the sinks registered for a profile.</summary>
/// <remarks>Delivery is ordered by registration order. A sink that is not registered for the profile never receives the event. The dispatcher reports required-sink failure in its result rather than throwing, so callers decide whether to fail closed.</remarks>
public interface IMemoryEventDispatcher
{
    /// <summary>Delivers one event to every sink registered for the profile.</summary>
    /// <param name="profile">The profile whose sinks observe the event.</param>
    /// <param name="memoryEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels delivery.</param>
    /// <returns>How many sinks recorded the event and how many failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="memoryEvent"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="profile"/> is blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryEventDispatchResult> PublishAsync(MemoryProfileKey profile, MemoryEvent memoryEvent, CancellationToken cancellationToken = default);
}
