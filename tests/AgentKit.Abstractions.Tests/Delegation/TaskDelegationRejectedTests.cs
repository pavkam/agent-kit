// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Delegation;



/// <summary>Verifies TaskDelegationRejected behavior and contracts.</summary>
public sealed class TaskDelegationRejectedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = DelegationId();
        var rejected = new TaskDelegationRejected(id, "denied");
        rejected.Id.ShouldBe(id);
        rejected.SafeMessage.ShouldBe("denied");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new TaskDelegationRejected(DelegationId(), " "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        TaskDelegationResult first = new TaskDelegationRejected(DelegationId(), "denied");
        TaskDelegationResult second = new TaskDelegationRejected(DelegationId(), "denied");
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesIndependentCopy()
    {
        var original = new TaskDelegationRejected(DelegationId(), "denied");
        var copy = original with { SafeMessage = "still denied" };
        copy.SafeMessage.ShouldBe("still denied");
        original.SafeMessage.ShouldBe("denied");
    }

    private static DelegationId DelegationId() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
}
