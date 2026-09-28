// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Resolves one fixed strategy instance for tests and legacy constructors.</summary>
internal sealed class FixedCompactionStrategyResolver(ICompactionStrategy strategy): ICompactionStrategyResolver
{
    private readonly ICompactionStrategy _strategy = strategy;

    /// <inheritdoc/>
    public ValueTask<CompactionStrategyResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionStrategyKey strategyKey,
        CancellationToken cancellationToken = default)
    {
        _ = compactorKey;
        _ = strategyKey;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<CompactionStrategyResolution>(new CompactionStrategyResolved(_strategy));
    }
}
