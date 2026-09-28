// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Resolves keyed summary generators registered for one compactor.</summary>
internal sealed class DefaultSummaryGeneratorResolver(
    ComponentKey<ICompactor> compactorKey,
    IServiceProvider services): ICompactionSummaryGeneratorResolver
{
    private readonly ComponentKey<ICompactor> _compactorKey = compactorKey;
    private readonly IServiceProvider _services = services;

    /// <inheritdoc/>
    public ValueTask<CompactionSummaryGeneratorResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionSummaryGeneratorKey generatorKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        ArgumentOutOfRangeException.ThrowIfEqual(generatorKey, default);
        cancellationToken.ThrowIfCancellationRequested();
        if (!compactorKey.Equals(_compactorKey))
        {
            return ValueTask.FromResult<CompactionSummaryGeneratorResolution>(
                new CompactionSummaryGeneratorNotFound(compactorKey, generatorKey));
        }

        var serviceKey = CompactionServiceKeys.SummaryGenerator(_compactorKey, generatorKey);
        var generator = _services.GetKeyedService<ICompactionSummaryGenerator>(serviceKey);
        return ValueTask.FromResult<CompactionSummaryGeneratorResolution>(
            generator is null
                ? new CompactionSummaryGeneratorNotFound(_compactorKey, generatorKey)
                : new CompactionSummaryGeneratorResolved(generator));
    }
}
