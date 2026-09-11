// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

public sealed class HaltRunTests
{
    [Fact]
    public void HaltRun_WhenOutcomeIsSuccessful_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => _ = new HaltRun(new AgentRunIdle()));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("outcome");
    }

}
