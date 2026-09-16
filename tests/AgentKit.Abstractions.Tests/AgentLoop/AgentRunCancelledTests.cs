// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunCancelled behavior and contracts.</summary>
public sealed class AgentRunCancelledTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunCancelled(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new AgentRunCancelled("cancelled");
        outcome.SafeMessage.ShouldBe("cancelled");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunCancelled("cancelled");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
