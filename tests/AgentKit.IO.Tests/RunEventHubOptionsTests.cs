// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventHubOptionsTests
{
    [Theory]
    [InlineData(0, 1, "maximumSubscriptions")]
    [InlineData(-1, 1, "maximumSubscriptions")]
    [InlineData(1, 0, "capacityPerSubscription")]
    [InlineData(1, -1, "capacityPerSubscription")]
    public void Constructor_WhenBoundsAreInvalid_ThrowsExactArgumentOutOfRange(int subscribers, int capacity, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventHubOptions(subscribers, capacity));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }
}
