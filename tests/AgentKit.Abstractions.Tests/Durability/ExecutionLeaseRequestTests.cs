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

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var address = DurabilityTestData.Address();
        var request = new ExecutionLeaseRequest(address, DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30));
        request.Address.ShouldBe(address);
        request.WorkerId.ShouldBe(DurabilityTestData.WorkerId);
        request.Duration.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void With_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var request = new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30));
        Should.Throw<ArgumentNullException>(() => _ = request with { Address = null! }).ParamName.ShouldBe("Address");
    }

    [Fact]
    public void With_WhenWorkerIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var request = new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30));
        Should.Throw<ArgumentOutOfRangeException>(() => _ = request with { WorkerId = default }).ParamName.ShouldBe("WorkerId");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30));
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenAddressIsValid_UpdatesAddress()
    {
        var original = new ExecutionLeaseRequest(DurabilityTestData.Address(), DurabilityTestData.WorkerId, TimeSpan.FromSeconds(30));
        var newAddress = DurabilityTestData.Address();
        var updated = original with { Address = newAddress };
        updated.Address.ShouldBe(newAddress);
    }
}
