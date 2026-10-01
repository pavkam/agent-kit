// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Denies a delegation.</summary>
public sealed record DelegationPolicyDenied: DelegationPolicyDecision
{
    /// <summary>Initializes a deny decision.</summary>
    /// <param name="rejection">The content-safe reason.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public DelegationPolicyDenied(DelegationRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public DelegationRejection Rejection { get; }
}
