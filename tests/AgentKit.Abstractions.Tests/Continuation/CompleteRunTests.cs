// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

using AgentKit.TestSupport;

public sealed class CompleteRunTests
{
    [Fact]
    public void CompleteRun_WhenOutcomeIsNotSuccessful_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => _ = new CompleteRun(new RunFailed(new RunFailure(RunResultTestData.Error(AgentErrorCodes.InvalidState)))));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompleteRun(new RunIdle());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new RunIdle();
        var decision = new CompleteRun(outcome);
        decision.Outcome.ShouldBe(outcome);
    }
}
