// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one registered compaction summary generator for a compactor key.</summary>
public interface ICompactionSummaryGeneratorResolver
{
    /// <summary>Resolves a generator registered for one compactor key.</summary>
    /// <param name="compactorKey">The compactor key owning the generator registration.</param>
    /// <param name="generatorKey">The generator key to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The closed resolution outcome.</returns>
    public ValueTask<CompactionSummaryGeneratorResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionSummaryGeneratorKey generatorKey,
        CancellationToken cancellationToken = default);
}
