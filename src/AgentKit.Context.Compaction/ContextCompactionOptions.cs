// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Engine-wide compaction ceilings configured at registration time.</summary>
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
}
