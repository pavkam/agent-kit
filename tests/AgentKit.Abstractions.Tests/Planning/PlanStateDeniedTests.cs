// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;



/// <summary>Verifies PlanStateDenied behavior and contracts.</summary>
public sealed class PlanStateDeniedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var denied = new PlanStateDenied("denied");
        denied.SafeMessage.ShouldBe("denied");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new PlanStateDenied(" "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new PlanStateDenied("denied");
        var copy = original with { SafeMessage = "still denied" };
        copy.SafeMessage.ShouldBe("still denied");
        original.SafeMessage.ShouldBe("denied");
    }
}
