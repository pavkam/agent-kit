// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome of resolving one compaction summary generator.</summary>
public abstract record CompactionSummaryGeneratorResolution;

/// <summary>A generator was resolved for the requested key.</summary>
/// <param name="Generator">The resolved generator instance.</param>
public sealed record CompactionSummaryGeneratorResolved(ICompactionSummaryGenerator Generator)
    : CompactionSummaryGeneratorResolution;

/// <summary>No generator is registered for the requested key under the compactor.</summary>
/// <param name="CompactorKey">The compactor key that was queried.</param>
/// <param name="GeneratorKey">The missing generator key.</param>
public sealed record CompactionSummaryGeneratorNotFound(
    ComponentKey<ICompactor> CompactorKey,
    CompactionSummaryGeneratorKey GeneratorKey)
    : CompactionSummaryGeneratorResolution;
