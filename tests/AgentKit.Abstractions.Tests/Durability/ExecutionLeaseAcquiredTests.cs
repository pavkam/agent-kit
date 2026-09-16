// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies ExecutionLeaseAcquired behavior and contracts.</summary>
public sealed class ExecutionLeaseAcquiredTests
{
    [Fact]
    public void ExecutionLeaseAcquired_Constructor_WhenLeaseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExecutionLeaseAcquired(null!));
        exception.ParamName.ShouldBe("lease");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsLease()
    {
        var lease = DurabilityTestData.Lease();
        var acquired = new ExecutionLeaseAcquired(lease);
        acquired.Lease.ShouldBeSameAs(lease);
        ExecutionLeaseResult result = acquired;
        _ = result.ShouldBeOfType<ExecutionLeaseAcquired>();
    }

    [Fact]
    public void With_WhenLeaseIsNull_ThrowsArgumentNullException()
    {
        var acquired = new ExecutionLeaseAcquired(DurabilityTestData.Lease());
        Should.Throw<ArgumentNullException>(() => _ = acquired with { Lease = null! }).ParamName.ShouldBe("Lease");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ExecutionLeaseAcquired(DurabilityTestData.Lease());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenLeaseIsValid_UpdatesLease()
    {
        var original = new ExecutionLeaseAcquired(DurabilityTestData.Lease());
        var newLease = DurabilityTestData.Lease();
        var updated = original with { Lease = newLease };
        updated.Lease.ShouldBeSameAs(newLease);
    }
}
