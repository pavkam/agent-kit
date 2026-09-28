// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Engine-wide durability ceilings and defaults.</summary>
public sealed class AgentDurabilityOptions
{
    /// <summary>Gets or sets the default execution-lease duration.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how often active leases are renewed.</summary>
    public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the maximum automatic recovery attempts per operation.</summary>
    public int MaximumRecoveryAttempts { get; set; } = 3;

    /// <summary>Gets or sets how unknown non-idempotent effects are handled.</summary>
    public UnknownEffectRecoveryMode UnknownEffectMode { get; set; } = UnknownEffectRecoveryMode.RequireOperator;

    /// <summary>Gets or sets the default checkpoint mode for coordinated operations.</summary>
    public DurableCheckpointMode CheckpointMode { get; set; } = DurableCheckpointMode.SemanticBoundaries;
}
