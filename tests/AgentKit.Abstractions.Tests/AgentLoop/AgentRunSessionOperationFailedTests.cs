// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunSessionOperationFailed behavior and contracts.</summary>
public sealed class AgentRunSessionOperationFailedTests
{
    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunSessionOperationFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new AgentRunSessionOperationFailed("failed");
        outcome.SafeMessage.ShouldBe("failed");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunSessionOperationFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
