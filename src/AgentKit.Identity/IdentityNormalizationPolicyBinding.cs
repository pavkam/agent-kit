// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Captures one materialized singleton normalization policy and its deterministic registration metadata.</summary>
internal sealed record IdentityNormalizationPolicyBinding
{
    /// <summary>Initializes a materialized normalization policy binding.</summary>
    /// <param name="policy">The normalization policy implementation.</param>
    /// <param name="registration">Its deterministic registration metadata.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public IdentityNormalizationPolicyBinding(IIdentityNormalizationPolicy policy, IdentityNormalizationPolicyRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(registration);
        Policy = policy;
        Registration = registration;
    }

    /// <summary>Gets the normalization policy implementation.</summary>
    public IIdentityNormalizationPolicy Policy { get; }

    /// <summary>Gets its deterministic registration metadata.</summary>
    public IdentityNormalizationPolicyRegistration Registration { get; }
}
