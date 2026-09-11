// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventSubscriptionRejectedExceptionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Constructor_WhenSubscriptionRejectionIsUndefined_RejectsExactArgument(int reason) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunEventSubscriptionRejectedException((RunEventSubscriptionRejection) reason)).ParamName.ShouldBe("reason");
}
