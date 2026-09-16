// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityDenial behavior and contracts.</summary>
public sealed class SecurityDenialTests
{
    [Fact]
    public void Constructor_WhenCodeIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityDenial(" ", "message")).ParamName.ShouldBe("code");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityDenial("code", " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var denial = new SecurityDenial("code", "message");
        denial.Code.ShouldBe("code");
        denial.SafeMessage.ShouldBe("message");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityDenial("code", "message");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
