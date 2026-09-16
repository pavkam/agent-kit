// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunOutputRejected behavior and contracts.</summary>
public sealed class AgentRunOutputRejectedTests
{
    [Fact]
    public void Constructor_WhenOutputRejectionIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunOutputRejected((OutputRejected) null!)).ParamName.ShouldBe("rejection");

    [Fact]
    public void Constructor_WhenConfigurationRejectionIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunOutputRejected((OutputConfigurationRejected) null!)).ParamName.ShouldBe("rejection");

    [Fact]
    public void Constructor_WhenOutputRejectionIsValid_RoundTripsProperties()
    {
        var rejection = LoopTestData.OutputRejected();
        var outcome = new AgentRunOutputRejected(rejection);
        outcome.Rejection.ShouldBe(rejection);
    }

    [Fact]
    public void Constructor_WhenConfigurationRejectionIsValid_RoundTripsProperties()
    {
        var rejection = LoopTestData.OutputConfigurationRejected();
        var outcome = new AgentRunOutputRejected(rejection);
        outcome.Rejection.ShouldBe(rejection);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunOutputRejected(LoopTestData.OutputRejected());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
