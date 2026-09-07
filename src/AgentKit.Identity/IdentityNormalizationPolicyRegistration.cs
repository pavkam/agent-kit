// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Declares the stable name and deterministic order of an additive normalization policy.</summary>
public sealed record IdentityNormalizationPolicyRegistration
{
    /// <summary>Initializes a policy registration.</summary>
    /// <param name="name">A unique, non-blank stable policy name.</param>
    /// <param name="order">The ascending order in which the policy transforms an issuer candidate.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    public IdentityNormalizationPolicyRegistration(string name, int order = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Order = order;
    }

    /// <summary>Gets the stable unique policy name.</summary>
    public string Name { get; }

    /// <summary>Gets the deterministic ascending policy order.</summary>
    public int Order { get; }
}
