// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes immutable artifact lifecycle events.</summary>
/// <remarks>A sink observes committed transitions and cannot affect lifecycle results. Implementations must not block indefinitely and receive no artifact content.</remarks>
public interface IArtifactEventSink
{
    /// <summary>Observes one event.</summary>
    /// <param name="artifactEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels delivery.</param>
    /// <returns>A task completed once the sink recorded the event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artifactEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken = default);
}
