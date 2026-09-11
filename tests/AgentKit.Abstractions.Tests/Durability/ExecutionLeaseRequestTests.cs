// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies ExecutionLeaseRequest behavior and contracts.</summary>
public sealed class ExecutionLeaseRequestTests
{
    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExecutionLeaseRequest(null!, DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30)));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenWorkerIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionLeaseRequest(DurabilityTestData.Address(), default, TimeSpan.FromSeconds(30)));
        exception.ParamName.ShouldBe("workerId");
    }

    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenDurationIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.Zero));
        exception.ParamName.ShouldBe("duration");
    }

    [Fact]
    public void ExecutionLeaseRequest_Constructor_WhenDurationIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.FromSeconds(-1)));
        exception.ParamName.ShouldBe("duration");
    }

    [Fact]
    public void ExecutionLeaseRequest_With_WhenDurationIsZero_Throws()
    {
        var request = new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30));
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => request with { Duration = TimeSpan.Zero });
        exception.ParamName.ShouldBe(nameof(ExecutionLeaseRequest.Duration));
    }
}
