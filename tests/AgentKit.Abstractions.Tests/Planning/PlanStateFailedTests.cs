// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies PlanStateFailed behavior and contracts.</summary>
public sealed class PlanStateFailedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var failed = new PlanStateFailed("failed");
        failed.SafeMessage.ShouldBe("failed");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new PlanStateFailed(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new PlanStateFailed("failed");
        var copy = original with { SafeMessage = "still failed" };
        copy.SafeMessage.ShouldBe("still failed");
        original.SafeMessage.ShouldBe("failed");
    }
}
