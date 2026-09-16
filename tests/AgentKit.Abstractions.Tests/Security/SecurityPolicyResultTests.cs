// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityPolicyResult behavior and contracts.</summary>
public sealed class SecurityPolicyResultTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecurityPolicyResult((SecurityPolicyResultKind) 99, "code", "message")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenNonAbstainingCodeIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityPolicyResult(SecurityPolicyResultKind.Allow, " ", "message")).ParamName.ShouldBe("code");

    [Fact]
    public void Constructor_WhenNonAbstainingMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "code", " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenAbstaining_AllowsNullCodeAndMessage()
    {
        var result = new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null);
        result.Kind.ShouldBe(SecurityPolicyResultKind.Abstain);
        result.Code.ShouldBeNull();
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "code", "message");
        result.Kind.ShouldBe(SecurityPolicyResultKind.Deny);
        result.Code.ShouldBe("code");
        result.SafeMessage.ShouldBe("message");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityPolicyResult(SecurityPolicyResultKind.RequireApproval, "code", "message");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
