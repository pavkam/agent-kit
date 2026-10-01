// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes immutable memory and retrieval events.</summary>
/// <remarks>A sink observes committed transitions and cannot affect policy, ranking, or results. Implementations must not block indefinitely and receive no content.</remarks>
public interface IMemoryEventSink
{
    /// <summary>Observes one event.</summary>
    /// <param name="memoryEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels delivery.</param>
    /// <returns>A task completed once the sink recorded the event.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="memoryEvent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default);
}
