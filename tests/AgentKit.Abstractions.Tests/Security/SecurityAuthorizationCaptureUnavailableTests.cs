// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Security;

/// <summary>Verifies SecurityAuthorizationCaptureUnavailable behavior and contracts.</summary>
public sealed class SecurityAuthorizationCaptureUnavailableTests
{
    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SecurityAuthorizationCaptureUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = new SecurityAuthorizationCaptureUnavailable("unavailable");
        result.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SecurityAuthorizationCaptureUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
