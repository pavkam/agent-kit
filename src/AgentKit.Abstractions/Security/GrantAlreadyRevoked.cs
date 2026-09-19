// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the grant was already revoked before this call, idempotently.</summary>
public sealed record GrantAlreadyRevoked: GrantRevocationResult
{
    /// <summary>Initializes an already-revoked result.</summary>
    /// <param name="grantId">The already-revoked grant identity.</param>
    public GrantAlreadyRevoked(GrantId grantId)
        : base(grantId)
    {
    }
}
