// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Immutable compaction options bound to one compactor registration.</summary>
public sealed record ContextCompactionOptionsSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="ContextCompactionOptionsSnapshot"/> record.</summary>
    public ContextCompactionOptionsSnapshot(
        ComponentKey<ICompactor> compactorKey,
        int maximumAttempts,
        int maximumSourceEntries,
        long maximumSourceBytes,
        int maximumSummaryTokens,
        int minimumRetainedEntries,
        int maximumValidationIssues,
        double minimumReductionRatio,
        TimeSpan attemptTimeout,
        bool persistRejectedCandidates)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        CompactorKey = compactorKey;
        MaximumAttempts = maximumAttempts;
        MaximumSourceEntries = maximumSourceEntries;
        MaximumSourceBytes = maximumSourceBytes;
        MaximumSummaryTokens = maximumSummaryTokens;
        MinimumRetainedEntries = minimumRetainedEntries;
        MaximumValidationIssues = maximumValidationIssues;
        MinimumReductionRatio = minimumReductionRatio;
        AttemptTimeout = attemptTimeout;
        PersistRejectedCandidates = persistRejectedCandidates;
    }

    /// <summary>Gets the compactor key.</summary>
    public ComponentKey<ICompactor> CompactorKey { get; }

    /// <summary>Gets the maximum attempts per logical checkpoint.</summary>
    public int MaximumAttempts { get; }

    /// <summary>Gets the maximum eligible source entries.</summary>
    public int MaximumSourceEntries { get; }

    /// <summary>Gets the maximum eligible source bytes.</summary>
    public long MaximumSourceBytes { get; }

    /// <summary>Gets the maximum summary output tokens.</summary>
    public int MaximumSummaryTokens { get; }

    /// <summary>Gets the minimum retained suffix entries.</summary>
    public int MinimumRetainedEntries { get; }

    /// <summary>Gets the maximum validation issues retained.</summary>
    public int MaximumValidationIssues { get; }

    /// <summary>Gets the minimum required reduction ratio.</summary>
    public double MinimumReductionRatio { get; }

    /// <summary>Gets the attempt timeout.</summary>
    public TimeSpan AttemptTimeout { get; }

    /// <summary>Gets a value indicating whether rejected candidates are persisted.</summary>
    public bool PersistRejectedCandidates { get; }
}
