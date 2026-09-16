// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityClaim behavior and contracts.</summary>
public sealed class IdentityClaimTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var issuer = new IdentityIssuerId("issuer");
        var claim = new IdentityClaim(issuer, "role", "reader", IdentityClaimValueKind.Text);
        claim.Issuer.ShouldBe(issuer);
        claim.Type.ShouldBe("role");
        claim.Value.ShouldBe("reader");
        claim.ValueKind.ShouldBe(IdentityClaimValueKind.Text);
    }

    [Fact]
    public void Constructor_WhenIssuerIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityClaim(default, "role", "reader", IdentityClaimValueKind.Text));
        exception.ParamName.ShouldBe("issuer");
    }

    [Fact]
    public void Constructor_WhenTypeIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityClaim(new IdentityIssuerId("issuer"), " ", "reader", IdentityClaimValueKind.Text));
        exception.ParamName.ShouldBe("type");
    }

    [Fact]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityClaim(new IdentityIssuerId("issuer"), "role", " ", IdentityClaimValueKind.Text));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueKindIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new IdentityClaim(new IdentityIssuerId("issuer"), "role", "reader", (IdentityClaimValueKind) 999));
        exception.ParamName.ShouldBe("valueKind");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var issuer = new IdentityIssuerId("issuer");
        var first = new IdentityClaim(issuer, "role", "reader", IdentityClaimValueKind.Text);
        var second = new IdentityClaim(issuer, "role", "reader", IdentityClaimValueKind.Text);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityClaim(new IdentityIssuerId("issuer"), "role", "reader", IdentityClaimValueKind.Text);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
