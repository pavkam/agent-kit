// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunAuthorizationUnavailable behavior and contracts.</summary>
public sealed class AgentRunAuthorizationUnavailableTests
{
    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunAuthorizationUnavailable(" ")).ParamName.ShouldBe("safeReason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new AgentRunAuthorizationUnavailable("unavailable");
        outcome.SafeReason.ShouldBe("unavailable");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunAuthorizationUnavailable("unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
