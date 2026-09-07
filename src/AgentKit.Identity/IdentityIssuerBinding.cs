// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Captures one instantiated singleton issuer and its registration key for immutable catalog construction.</summary>
internal sealed record IdentityIssuerBinding
{
    /// <summary>Initializes a materialized issuer binding.</summary>
    /// <param name="issuer">The trusted issuer implementation.</param>
    /// <param name="registration">Its stable keyed registration.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public IdentityIssuerBinding(IIdentityIssuer issuer, IdentityIssuerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(registration);
        Issuer = issuer;
        Registration = registration;
    }

    /// <summary>Gets the trusted issuer implementation.</summary>
    public IIdentityIssuer Issuer { get; }

    /// <summary>Gets the issuer's stable keyed registration.</summary>
    public IdentityIssuerRegistration Registration { get; }
}
