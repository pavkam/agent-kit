// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ExternalIdentityClaim behavior and contracts.</summary>
public sealed class ExternalIdentityClaimTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var claim = new ExternalIdentityClaim("role", "reader", IdentityClaimValueKind.Text);
        claim.Type.ShouldBe("role");
        claim.Value.ShouldBe("reader");
        claim.ValueKind.ShouldBe(IdentityClaimValueKind.Text);
    }

    [Fact]
    public void Constructor_WhenTypeIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ExternalIdentityClaim(" ", "reader", IdentityClaimValueKind.Text));
        exception.ParamName.ShouldBe("type");
    }

    [Fact]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ExternalIdentityClaim("role", " ", IdentityClaimValueKind.Text));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueKindIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExternalIdentityClaim("role", "reader", (IdentityClaimValueKind) 999));
        exception.ParamName.ShouldBe("valueKind");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new ExternalIdentityClaim("role", "reader", IdentityClaimValueKind.Text);
        var second = new ExternalIdentityClaim("role", "reader", IdentityClaimValueKind.Text);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ExternalIdentityClaim("role", "reader", IdentityClaimValueKind.Text);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
