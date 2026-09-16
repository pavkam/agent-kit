// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputRetryPolicy behavior and contracts.</summary>
public sealed class OutputRetryPolicyTests
{
    [Fact]
    public void Constructor_WhenMaximumAttemptsIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OutputRetryPolicy(-1)).ParamName.ShouldBe("maximumAttempts");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsMaximumAttempts()
    {
        var policy = new OutputRetryPolicy(3);
        policy.MaximumAttempts.ShouldBe(3);
    }

    [Fact]
    public void None_WhenAccessed_HasZeroMaximumAttempts() =>
        OutputRetryPolicy.None.MaximumAttempts.ShouldBe(0);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputRetryPolicy(3);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
