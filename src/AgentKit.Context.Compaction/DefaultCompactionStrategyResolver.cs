// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Resolves keyed compaction strategies registered for one compactor.</summary>
internal sealed class DefaultCompactionStrategyResolver(
    ComponentKey<ICompactor> compactorKey,
    IServiceProvider services): ICompactionStrategyResolver
{
    private readonly ComponentKey<ICompactor> _compactorKey = compactorKey;
    private readonly IServiceProvider _services = services;

    /// <inheritdoc/>
    public ValueTask<CompactionStrategyResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionStrategyKey strategyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        ArgumentOutOfRangeException.ThrowIfEqual(strategyKey, default);
        cancellationToken.ThrowIfCancellationRequested();
        if (!compactorKey.Equals(_compactorKey))
        {
            return ValueTask.FromResult<CompactionStrategyResolution>(
                new CompactionStrategyNotFound(compactorKey, strategyKey));
        }

        var serviceKey = CompactionServiceKeys.Strategy(_compactorKey, strategyKey);
        var strategy = _services.GetKeyedService<ICompactionStrategy>(serviceKey);
        return ValueTask.FromResult<CompactionStrategyResolution>(
            strategy is null
                ? new CompactionStrategyNotFound(_compactorKey, strategyKey)
                : new CompactionStrategyResolved(strategy));
    }
}
