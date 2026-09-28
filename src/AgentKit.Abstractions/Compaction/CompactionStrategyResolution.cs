// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome of resolving one compaction strategy.</summary>
public abstract record CompactionStrategyResolution;

/// <summary>A strategy was resolved for the requested key.</summary>
/// <param name="Strategy">The resolved strategy instance.</param>
public sealed record CompactionStrategyResolved(ICompactionStrategy Strategy): CompactionStrategyResolution;

/// <summary>No strategy is registered for the requested key under the compactor.</summary>
/// <param name="CompactorKey">The compactor key that was queried.</param>
/// <param name="StrategyKey">The missing strategy key.</param>
public sealed record CompactionStrategyNotFound(
    ComponentKey<ICompactor> CompactorKey,
    CompactionStrategyKey StrategyKey): CompactionStrategyResolution;
