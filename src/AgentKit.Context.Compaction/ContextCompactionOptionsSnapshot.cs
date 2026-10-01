// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Immutable compaction options bound to one compactor registration.</summary>
public sealed record ContextCompactionOptionsSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="ContextCompactionOptionsSnapshot"/> record.</summary>
    /// <param name="compactorKey">The nondefault compactor key this snapshot is bound to.</param>
    /// <param name="maximumAttempts">The maximum attempts per logical checkpoint.</param>
    /// <param name="maximumSourceEntries">The maximum eligible source entries.</param>
    /// <param name="maximumSourceBytes">The maximum eligible source bytes.</param>
    /// <param name="maximumSummaryTokens">The maximum summary output tokens.</param>
    /// <param name="minimumRetainedEntries">The minimum retained suffix entries.</param>
    /// <param name="maximumValidationIssues">The maximum validation issues retained.</param>
    /// <param name="minimumReductionRatio">The minimum required reduction ratio.</param>
    /// <param name="attemptTimeout">The attempt timeout.</param>
    /// <param name="persistRejectedCandidates">Whether rejected candidates are persisted.</param>
    /// <param name="defaultStrategyOrder">The ordered strategy keys applied to a request that carries no policy snapshot.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactorKey"/> is the default value.</exception>
    /// <exception cref="ArgumentException"><paramref name="defaultStrategyOrder"/> is uninitialized, empty, or contains a default or duplicate key.</exception>
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
        bool persistRejectedCandidates,
        ImmutableArray<CompactionStrategyKey> defaultStrategyOrder)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        ArgumentException.ThrowIfDefault(defaultStrategyOrder);
        if (defaultStrategyOrder.IsEmpty)
        {
            throw new ArgumentException("The default strategy order must name at least one strategy.", nameof(defaultStrategyOrder));
        }

        if (defaultStrategyOrder.Contains(default) || defaultStrategyOrder.Distinct().Count() != defaultStrategyOrder.Length)
        {
            throw new ArgumentException(
                "The default strategy order must not contain default or duplicate keys.", nameof(defaultStrategyOrder));
        }

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
        DefaultStrategyOrder = defaultStrategyOrder;
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

    /// <summary>Gets the ordered strategy keys applied to a request that carries no policy snapshot.</summary>
    public ImmutableArray<CompactionStrategyKey> DefaultStrategyOrder { get; }
}
