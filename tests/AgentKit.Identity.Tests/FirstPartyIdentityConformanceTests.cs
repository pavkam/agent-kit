// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Runs reusable issuer contract cases against the first-party test issuer.</summary>
public sealed class FirstPartyIdentityIssuerConformanceTests: IdentityIssuerConformanceTests<IdentityIssuerConformanceFixture>
{
    /// <inheritdoc/>
    protected override IdentityIssuerConformanceFixture CreateFixture() => new();
}

/// <summary>Runs reusable validation-policy contract cases against the first-party policy.</summary>
public sealed class FirstPartyIdentityValidationPolicyConformanceTests: IdentityValidationPolicyConformanceTests<IdentityValidationPolicyConformanceFixture>
{
    /// <inheritdoc/>
    protected override IdentityValidationPolicyConformanceFixture CreateFixture() => new();
}

/// <summary>Runs reusable deriver contract cases against the first-party deriver.</summary>
public sealed class FirstPartyDelegatedIdentityDeriverConformanceTests: DelegatedIdentityDeriverConformanceTests<DelegatedIdentityDeriverConformanceFixture>
{
    /// <inheritdoc/>
    protected override DelegatedIdentityDeriverConformanceFixture CreateFixture() => new();
}
