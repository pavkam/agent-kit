// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Delivers immutable artifact events to the sinks registered for one coordinator.</summary>
/// <remarks>Delivery is deterministic by registration order and lifetime-aware, so a coordinator never captures a scoped sink. Sink failure is isolated and never changes the lifecycle outcome; cancellation always propagates.</remarks>
public interface IArtifactEventDispatcher
{
    /// <summary>Delivers one event to every sink registered for the coordinator.</summary>
    /// <param name="coordinatorKey">The coordinator whose sinks observe the event.</param>
    /// <param name="artifactEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels delivery.</param>
    /// <returns>A task completed once every registered sink was offered the event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artifactEvent"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="coordinatorKey"/> is blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask PublishAsync(
        ComponentKey<IArtifactCoordinator> coordinatorKey,
        ArtifactEvent artifactEvent,
        CancellationToken cancellationToken = default);
}
