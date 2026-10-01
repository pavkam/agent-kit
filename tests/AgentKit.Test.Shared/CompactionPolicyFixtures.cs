// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds valid <see cref="CompactionPolicySnapshot"/> and <see cref="CompactionProfilePublication"/> values for tests.</summary>
public static class CompactionPolicyFixtures
{
    /// <summary>The profile key the default fixture policy belongs to.</summary>
    public static CompactionProfileKey ProfileKey { get; } = new("compaction-profile");

    /// <summary>The compactor key the default fixture policy selects.</summary>
    public static ComponentKey<ICompactor> CompactorKey { get; } = new("compactor");

    /// <summary>The strategy key the default fixture policy orders.</summary>
    public static CompactionStrategyKey StrategyKey { get; } = new("fixture.strategy");

    /// <summary>Builds a valid policy snapshot.</summary>
    /// <param name="profileKey">The owning profile, or <see langword="null"/> for <see cref="ProfileKey"/>.</param>
    /// <param name="compactorKey">The selected compactor, or <see langword="null"/> for <see cref="CompactorKey"/>.</param>
    /// <param name="strategyOrder">The ordered strategy keys, or <see langword="null"/> for <see cref="StrategyKey"/> alone.</param>
    /// <returns>A snapshot with in-range bounds.</returns>
    public static CompactionPolicySnapshot Create(
        CompactionProfileKey? profileKey = null,
        ComponentKey<ICompactor>? compactorKey = null,
        ImmutableArray<CompactionStrategyKey>? strategyOrder = null) =>
        new(
            profileKey ?? ProfileKey,
            new CompactionProfileVersion(1),
            compactorKey ?? CompactorKey,
            strategyOrder ?? [StrategyKey],
            maximumAttempts: 2,
            maximumSourceEntries: 100,
            maximumSourceBytes: 1_024,
            maximumSummaryTokens: 256,
            minimumRetainedEntries: 1,
            maximumValidationIssues: 8,
            minimumReductionRatio: 0.2,
            allowOversizedTurnRepair: false,
            persistRejectedCandidates: false,
            new ContentHash("sha256:fixture-compaction-policy"));

    /// <summary>Builds a publication over <see cref="Create"/>.</summary>
    /// <param name="enabled">Whether compaction runs under the profile.</param>
    /// <param name="profileKey">The owning profile, or <see langword="null"/> for <see cref="ProfileKey"/>.</param>
    /// <param name="compactorKey">The selected compactor, or <see langword="null"/> for <see cref="CompactorKey"/>.</param>
    /// <returns>An immutable publication.</returns>
    public static CompactionProfilePublication Publication(
        bool enabled = true,
        CompactionProfileKey? profileKey = null,
        ComponentKey<ICompactor>? compactorKey = null) =>
        new(Create(profileKey, compactorKey), enabled);
}
