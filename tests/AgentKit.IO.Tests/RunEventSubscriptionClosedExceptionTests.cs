// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventSubscriptionClosedExceptionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Constructor_WhenClosureIsNotDeliveryFailure_RejectsExactArgument(int state) =>
        Should.Throw<ArgumentException>(() => new RunEventSubscriptionClosedException((RunEventSubscriptionState) state, null)).ParamName.ShouldBe("state");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenUnavailableSequenceIsInvalid_RejectsExactArgument(long sequence) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunEventSubscriptionClosedException(RunEventSubscriptionState.SlowConsumer, sequence)).ParamName.ShouldBe("firstUnavailableSequence");
}
