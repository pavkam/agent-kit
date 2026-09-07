// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Immutable validated identity options captured once when the service provider is built.</summary>
internal sealed record AgentIdentityOptionsSnapshot
{
    /// <summary>Captures identity options after composition validation.</summary>
    /// <param name="allowAnonymous">Whether anonymous trusted assertions may execute.</param>
    /// <param name="maximumDelegationDepth">The positive maximum retained delegation depth.</param>
    /// <param name="maximumClockSkew">The nonnegative evidence clock tolerance.</param>
    /// <param name="maximumEvidenceLifetime">The positive maximum evidence lifetime.</param>
    /// <exception cref="ArgumentOutOfRangeException">A numeric or duration constraint is invalid.</exception>
    public AgentIdentityOptionsSnapshot(bool allowAnonymous, int maximumDelegationDepth, TimeSpan maximumClockSkew, TimeSpan maximumEvidenceLifetime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDelegationDepth);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumClockSkew, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumEvidenceLifetime, TimeSpan.Zero);
        AllowAnonymous = allowAnonymous;
        MaximumDelegationDepth = maximumDelegationDepth;
        MaximumClockSkew = maximumClockSkew;
        MaximumEvidenceLifetime = maximumEvidenceLifetime;
    }

    /// <summary>Gets whether anonymous trusted assertions may execute.</summary>
    public bool AllowAnonymous { get; }

    /// <summary>Gets the maximum retained delegation depth.</summary>
    public int MaximumDelegationDepth { get; }

    /// <summary>Gets the evidence clock tolerance.</summary>
    public TimeSpan MaximumClockSkew { get; }

    /// <summary>Gets the maximum evidence lifetime.</summary>
    public TimeSpan MaximumEvidenceLifetime { get; }
}
