// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>The immutable values <see cref="CompactionProfileOptions"/> held when <c>AddCompactionProfile</c> captured them.</summary>
/// <param name="Key">The nondefault profile key.</param>
/// <param name="CompactorKey">The nondefault compactor key the profile selects.</param>
/// <param name="Version">The profile version.</param>
/// <param name="Enabled">Whether compaction runs under the profile.</param>
/// <param name="StrategyOrder">The non-empty, duplicate-free ordered strategy keys.</param>
/// <param name="AllowOversizedTurnRepair">Whether oversized-turn repair is allowed.</param>
internal sealed record CompactionProfileDeclaration(
    CompactionProfileKey Key,
    ComponentKey<ICompactor> CompactorKey,
    CompactionProfileVersion Version,
    bool Enabled,
    ImmutableArray<CompactionStrategyKey> StrategyOrder,
    bool AllowOversizedTurnRepair);
