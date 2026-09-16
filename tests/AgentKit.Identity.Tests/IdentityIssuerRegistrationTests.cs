// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;



/// <summary>Verifies IdentityIssuerRegistration behavior and contracts.</summary>
public sealed class IdentityIssuerRegistrationTests
{
    [Fact]
    public void IdentityIssuerRegistration_WhenIssuerIsDefault_ThrowsWithIssuerIdParameterName()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityIssuerRegistration(default));
        exception.ParamName.ShouldBe("issuerId");
    }

    [Fact]
    public void Equality_WhenIssuerIdsMatch_TreatsInstancesAsEqual()
    {
        var first = new IdentityIssuerRegistration(new IdentityIssuerId("issuer"));
        var second = new IdentityIssuerRegistration(new IdentityIssuerId("issuer"));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.IssuerId.ShouldBe(new IdentityIssuerId("issuer"));
        first.ToString().ShouldContain(nameof(IdentityIssuerRegistration));
        (first with { }).ShouldBe(first);
    }
}
