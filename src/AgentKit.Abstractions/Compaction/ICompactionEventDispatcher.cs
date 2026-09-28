// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Routes compaction events to sinks registered for one compactor key.</summary>
public interface ICompactionEventDispatcher
{
    /// <summary>Publishes one compaction event through the configured sinks.</summary>
    /// <param name="compactorKey">The compactor key whose sinks receive the event.</param>
    /// <param name="compactionEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token used to cancel publication.</param>
    /// <returns>The closed dispatch outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="compactionEvent"/> is null.</exception>
    public ValueTask<CompactionEventDispatchResult> PublishAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionEvent compactionEvent,
        CancellationToken cancellationToken = default);
}
