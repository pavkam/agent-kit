// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Produces checkpoint content for a structurally safe cut already selected
/// by an <see cref="ICompactionCutSelector"/>.
/// </summary>
/// <remarks>
/// A strategy trusts the cut it is given; it does not re-derive or
/// second-guess structural boundaries. Implementations must be safe to call
/// concurrently for independent requests and must not mutate any input they
/// receive. A strategy never invokes session or provider egress directly on
/// its own authority; any provider call it needs is issued through the
/// caller-supplied request context so authorization and budget accounting
/// stay centralized.
/// </remarks>
public interface ICompactionStrategy
{
    /// <summary>Produces checkpoint content for one compaction attempt.</summary>
    /// <param name="request">The checkpoint-production request.</param>
    /// <param name="cancellationToken">A token used to cancel production.</param>
    /// <returns>
    /// A task that resolves to the closed production outcome: a produced
    /// checkpoint, a deliberate decline, or an unexpected failure.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request, CancellationToken cancellationToken = default);
}
