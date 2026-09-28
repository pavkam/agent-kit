// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Resolves one fixed summary generator instance for tests.</summary>
internal sealed class FixedCompactionSummaryGeneratorResolver(ICompactionSummaryGenerator generator)
    : ICompactionSummaryGeneratorResolver
{
    private readonly ICompactionSummaryGenerator _generator = generator;

    /// <inheritdoc/>
    public ValueTask<CompactionSummaryGeneratorResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionSummaryGeneratorKey generatorKey,
        CancellationToken cancellationToken = default)
    {
        _ = compactorKey;
        _ = generatorKey;
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<CompactionSummaryGeneratorResolution>(
            new CompactionSummaryGeneratorResolved(_generator));
    }
}
