// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the resolved retention decision carried by every artifact reference.</summary>
public sealed record ArtifactRetention
{
    /// <summary>Initializes a retention decision.</summary>
    /// <param name="policy">The selected policy.</param>
    /// <param name="expiresAt">The expiry, or null for policy-controlled indefinite retention.</param>
    /// <param name="legalHold">Whether ordinary deletion is prohibited.</param>
    public ArtifactRetention(ArtifactRetentionPolicyKey policy, DateTimeOffset? expiresAt, bool legalHold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.Value, nameof(policy));
        Policy = policy;
        ExpiresAt = expiresAt;
        LegalHold = legalHold;
    }
    /// <summary>Gets the selected policy.</summary>
    public ArtifactRetentionPolicyKey Policy { get; }
    /// <summary>Gets the resolved expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; }
    /// <summary>Gets whether ordinary deletion is prohibited.</summary>
    public bool LegalHold { get; }
}
