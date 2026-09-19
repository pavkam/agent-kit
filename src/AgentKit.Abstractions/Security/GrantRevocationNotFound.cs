// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no grant with the requested identity is registered.</summary>
public sealed record GrantRevocationNotFound: GrantRevocationResult
{
    /// <summary>Initializes a not-found revocation result.</summary>
    /// <param name="grantId">The unregistered grant identity.</param>
    public GrantRevocationNotFound(GrantId grantId)
        : base(grantId)
    {
    }
}
