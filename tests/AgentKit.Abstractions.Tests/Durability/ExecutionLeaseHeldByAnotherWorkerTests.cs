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
}
