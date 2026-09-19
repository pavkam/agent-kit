// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a registered grant was revoked by this call.</summary>
public sealed record GrantRevoked: GrantRevocationResult
{
    /// <summary>Initializes a successful revocation result.</summary>
    /// <param name="grantId">The revoked grant identity.</param>
    /// <param name="reason">The reason recorded for the revocation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public GrantRevoked(GrantId grantId, RevocationReason reason)
        : base(grantId)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Reason = reason;
    }

    /// <summary>Gets the recorded revocation reason.</summary>
    public RevocationReason Reason { get; init; }
}
