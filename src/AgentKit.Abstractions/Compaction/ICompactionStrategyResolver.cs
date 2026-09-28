// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one registered compaction strategy for a compactor key.</summary>
public interface ICompactionStrategyResolver
{
    /// <summary>Resolves a strategy registered for one compactor key.</summary>
    /// <param name="compactorKey">The compactor key owning the strategy registration.</param>
    /// <param name="strategyKey">The strategy key to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The closed resolution outcome.</returns>
    public ValueTask<CompactionStrategyResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionStrategyKey strategyKey,
        CancellationToken cancellationToken = default);
}
