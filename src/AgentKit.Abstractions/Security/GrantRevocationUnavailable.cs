// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that revocation could not complete against the authoritative grant store.</summary>
public sealed record GrantRevocationUnavailable: GrantRevocationResult
{
    /// <summary>Initializes an unavailable revocation result.</summary>
    /// <param name="grantId">The grant identity the caller attempted to revoke.</param>
    /// <param name="safeReason">The non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public GrantRevocationUnavailable(GrantId grantId, string safeReason)
        : base(grantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the non-sensitive unavailable reason.</summary>
    public string SafeReason { get; init; }
}
