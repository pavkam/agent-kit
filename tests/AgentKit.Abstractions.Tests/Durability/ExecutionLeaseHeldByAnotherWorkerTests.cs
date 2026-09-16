// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies ExecutionLeaseHeldByAnotherWorker behavior and contracts.</summary>
public sealed class ExecutionLeaseHeldByAnotherWorkerTests
{
    [Fact]
    public void ExecutionLeaseHeldByAnotherWorker_Constructor_WhenOwnerIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionLeaseHeldByAnotherWorker(default, DurabilityTestData.Token, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("currentOwnerWorkerId");
    }

    [Fact]
    public void ExecutionLeaseHeldByAnotherWorker_Constructor_WhenTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionLeaseHeldByAnotherWorker(DurabilityTestData.WorkerId, default, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("currentToken");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var held = new ExecutionLeaseHeldByAnotherWorker(DurabilityTestData.WorkerId, DurabilityTestData.Token, DurabilityTestData.Now);
        held.CurrentOwnerWorkerId.ShouldBe(DurabilityTestData.WorkerId);
        held.CurrentToken.ShouldBe(DurabilityTestData.Token);
        held.ExpiresAt.ShouldBe(DurabilityTestData.Now);
        ExecutionLeaseResult result = held;
        _ = result.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ExecutionLeaseHeldByAnotherWorker(DurabilityTestData.WorkerId, DurabilityTestData.Token, DurabilityTestData.Now);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
