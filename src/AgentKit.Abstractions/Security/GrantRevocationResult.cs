// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed terminal outcome of attempting to revoke one bounded security grant.</summary>
public abstract record GrantRevocationResult
{
    private protected GrantRevocationResult(GrantId grantId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(grantId, default);
        GrantId = grantId;
    }

    /// <summary>Gets the grant identity the caller attempted to revoke.</summary>
    public GrantId GrantId { get; init; }
}
