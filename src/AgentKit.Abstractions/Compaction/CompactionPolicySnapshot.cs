// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutable compaction policy compiled for one attempt.</summary>
public sealed record CompactionPolicySnapshot
{
    /// <summary>Initializes a validated policy snapshot.</summary>
    /// <param name="profileKey">The profile that owns this policy.</param>
    /// <param name="profileVersion">The profile version.</param>
    /// <param name="compactorKey">The compactor key selected by the profile.</param>
    /// <param name="strategyOrder">The ordered strategy keys to attempt.</param>
    /// <param name="maximumAttempts">The maximum attempts allowed for one logical checkpoint.</param>
    /// <param name="maximumSourceEntries">The maximum eligible source entries.</param>
    /// <param name="maximumSourceBytes">The maximum eligible source bytes.</param>
    /// <param name="maximumSummaryTokens">The maximum summary output tokens.</param>
    /// <param name="minimumRetainedEntries">The minimum retained suffix entries.</param>
    /// <param name="maximumValidationIssues">The maximum validation issues to retain.</param>
    /// <param name="minimumReductionRatio">The minimum required reduction ratio.</param>
    /// <param name="allowOversizedTurnRepair">Whether oversized-turn repair is allowed.</param>
    /// <param name="persistRejectedCandidates">Whether rejected candidates are persisted.</param>
    /// <param name="configurationFingerprint">The configuration fingerprint for this snapshot.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound or identity is invalid.</exception>
    /// <exception cref="ArgumentException">The strategy order is empty or contains duplicates.</exception>
    public CompactionPolicySnapshot(
        CompactionProfileKey profileKey,
        CompactionProfileVersion profileVersion,
        ComponentKey<ICompactor> compactorKey,
        ImmutableArray<CompactionStrategyKey> strategyOrder,
        int maximumAttempts,
        int maximumSourceEntries,
        long maximumSourceBytes,
        int maximumSummaryTokens,
        int minimumRetainedEntries,
        int maximumValidationIssues,
        double minimumReductionRatio,
        bool allowOversizedTurnRepair,
        bool persistRejectedCandidates,
        ContentHash configurationFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileKey, default);
        ArgumentOutOfRangeException.ThrowIfEqual(profileVersion, default);
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        ArgumentException.ThrowIfDefault(strategyOrder);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAttempts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSourceEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSourceBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSummaryTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumRetainedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumValidationIssues);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumReductionRatio, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minimumReductionRatio, 1d);
        ArgumentOutOfRangeException.ThrowIfEqual(configurationFingerprint, default);

        if (strategyOrder.Length == 0)
        {
            throw new ArgumentException("Strategy order must name at least one strategy.", nameof(strategyOrder));
        }

        if (strategyOrder.Distinct().Count() != strategyOrder.Length)
        {
            throw new ArgumentException("Strategy order must not contain duplicate keys.", nameof(strategyOrder));
        }

        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        CompactorKey = compactorKey;
        StrategyOrder = strategyOrder;
        MaximumAttempts = maximumAttempts;
        MaximumSourceEntries = maximumSourceEntries;
        MaximumSourceBytes = maximumSourceBytes;
        MaximumSummaryTokens = maximumSummaryTokens;
        MinimumRetainedEntries = minimumRetainedEntries;
        MaximumValidationIssues = maximumValidationIssues;
        MinimumReductionRatio = minimumReductionRatio;
        AllowOversizedTurnRepair = allowOversizedTurnRepair;
        PersistRejectedCandidates = persistRejectedCandidates;
        ConfigurationFingerprint = configurationFingerprint;
    }

    /// <summary>Gets the profile that owns this policy.</summary>
    public CompactionProfileKey ProfileKey { get; }

    /// <summary>Gets the profile version.</summary>
    public CompactionProfileVersion ProfileVersion { get; }

    /// <summary>Gets the compactor key selected by the profile.</summary>
    public ComponentKey<ICompactor> CompactorKey { get; }

    /// <summary>Gets the ordered strategy keys to attempt.</summary>
    public ImmutableArray<CompactionStrategyKey> StrategyOrder { get; }

    /// <summary>Gets the maximum attempts allowed for one logical checkpoint.</summary>
    public int MaximumAttempts { get; }

    /// <summary>Gets the maximum eligible source entries.</summary>
    public int MaximumSourceEntries { get; }

    /// <summary>Gets the maximum eligible source bytes.</summary>
    public long MaximumSourceBytes { get; }

    /// <summary>Gets the maximum summary output tokens.</summary>
    public int MaximumSummaryTokens { get; }

    /// <summary>Gets the minimum retained suffix entries.</summary>
    public int MinimumRetainedEntries { get; }

    /// <summary>Gets the maximum validation issues to retain.</summary>
    public int MaximumValidationIssues { get; }

    /// <summary>Gets the minimum required reduction ratio.</summary>
    public double MinimumReductionRatio { get; }

    /// <summary>Gets a value indicating whether oversized-turn repair is allowed.</summary>
    public bool AllowOversizedTurnRepair { get; }

    /// <summary>Gets a value indicating whether rejected candidates are persisted.</summary>
    public bool PersistRejectedCandidates { get; }

    /// <summary>Gets the configuration fingerprint for this snapshot.</summary>
    public ContentHash ConfigurationFingerprint { get; }
}
