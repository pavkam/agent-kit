// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Engine-wide compaction ceilings configured at registration time for one compactor key.</summary>
/// <remarks>
/// Instances are bound as named options under the compactor key's text, so two compactors registered in one engine never
/// share or overwrite each other's ceilings. Registration validates every bound when the options are first read.
/// </remarks>
public sealed class ContextCompactionOptions
{
    /// <summary>Gets or sets the maximum attempts per logical checkpoint.</summary>
    public int MaximumAttempts { get; set; } = 2;

    /// <summary>Gets or sets the maximum eligible source entries.</summary>
    public int MaximumSourceEntries { get; set; } = 2_048;

    /// <summary>Gets or sets the maximum eligible source bytes.</summary>
    public long MaximumSourceBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Gets or sets the maximum summary output tokens.</summary>
    public int MaximumSummaryTokens { get; set; } = 2_048;

    /// <summary>Gets or sets the minimum retained suffix entries.</summary>
    public int MinimumRetainedEntries { get; set; } = 8;

    /// <summary>Gets or sets the maximum validation issues retained.</summary>
    public int MaximumValidationIssues { get; set; } = 64;

    /// <summary>Gets or sets the minimum required reduction ratio.</summary>
    public double MinimumReductionRatio { get; set; } = 0.20;

    /// <summary>Gets or sets the attempt timeout.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets a value indicating whether rejected candidates are persisted.</summary>
    public bool PersistRejectedCandidates { get; set; }

    /// <summary>Gets or sets the ordered strategy keys the compactor applies to a request that carries no policy snapshot.</summary>
    /// <value>
    /// A non-empty, duplicate-free list of keys registered for the compactor; <see cref="CompactionStrategyKeys.Extractive"/>
    /// by default. A request that carries a profile's <see cref="CompactionPolicySnapshot"/> uses that snapshot's order
    /// instead.
    /// </value>
    public List<CompactionStrategyKey> DefaultStrategyOrder { get; set; } = [CompactionStrategyKeys.Extractive];
}
