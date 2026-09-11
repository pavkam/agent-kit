// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

public sealed class CompleteRunTests
{
    [Fact]
    public void CompleteRun_WhenOutcomeIsNotSuccessful_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => _ = new CompleteRun(new AgentRunTurnLimitReached(1)));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("outcome");
    }

}
