// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Receives immutable compaction observation events.</summary>
public interface ICompactionEventSink
{
    /// <summary>Publishes one compaction event.</summary>
    /// <param name="compactionEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token used to cancel publication.</param>
    /// <returns>A task that completes when publication finishes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="compactionEvent"/> is null.</exception>
    public ValueTask PublishAsync(CompactionEvent compactionEvent, CancellationToken cancellationToken = default);
}
