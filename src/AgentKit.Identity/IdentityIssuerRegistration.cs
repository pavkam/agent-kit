// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Declares the stable key under which one trusted issuer is registered.</summary>
public sealed record IdentityIssuerRegistration
{
    /// <summary>Initializes a keyed issuer registration.</summary>
    /// <param name="issuerId">The non-empty issuer key accepted by the resolver.</param>
    /// <exception cref="ArgumentException"><paramref name="issuerId"/> is the default issuer identifier.</exception>
    public IdentityIssuerRegistration(IdentityIssuerId issuerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId.Value, nameof(issuerId));
        IssuerId = issuerId;
    }

    /// <summary>Gets the issuer key accepted by this registration.</summary>
    public IdentityIssuerId IssuerId { get; }
}
