// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityFailure behavior and contracts.</summary>
public sealed class IdentityFailureTests
{
    [Fact]
    public void IdentityFailure_WhenMessageIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityFailure(IdentityFailureKind.Malformed, " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var issuer = new IdentityIssuerId("issuer");
        var failure = new IdentityFailure(IdentityFailureKind.Malformed, "malformed", issuer);
        failure.Kind.ShouldBe(IdentityFailureKind.Malformed);
        failure.SafeMessage.ShouldBe("malformed");
        failure.Issuer.ShouldBe(issuer);
    }

    [Fact]
    public void Constructor_WhenIssuerIsOmitted_DefaultsToNull() =>
        new IdentityFailure(IdentityFailureKind.Malformed, "malformed").Issuer.ShouldBeNull();

    [Fact]
    public void Constructor_WhenIssuerIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityFailure(IdentityFailureKind.Malformed, "malformed", default(IdentityIssuerId)));
        exception.ParamName.ShouldBe("issuer");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityFailure(IdentityFailureKind.Malformed, "malformed");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
