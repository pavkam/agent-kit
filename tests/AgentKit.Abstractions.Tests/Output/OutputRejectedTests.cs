// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputRejected behavior and contracts.</summary>
public sealed class OutputRejectedTests
{
    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new OutputRejected(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsFailure()
    {
        var failure = OutputTestData.Failure();
        var rejected = new OutputRejected(failure);
        rejected.Failure.ShouldBe(failure);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputRejected(OutputTestData.Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
