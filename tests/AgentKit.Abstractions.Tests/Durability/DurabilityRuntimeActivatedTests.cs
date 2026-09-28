// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurabilityRuntimeActivated"/> behavior and contracts.</summary>
public sealed class DurabilityRuntimeActivatedTests
{
    [Fact]
    public void Constructor_WhenLeaseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurabilityRuntimeActivated(null!));

        exception.ParamName.ShouldBe("lease");
    }

    [Fact]
    public void Constructor_WhenLeaseIsSupplied_RetainsItAsAnActivationResult()
    {
        var lease = DurabilityTestData.RuntimeLease();

        var activated = new DurabilityRuntimeActivated(lease);

        activated.Lease.ShouldBeSameAs(lease);
        _ = activated.ShouldBeAssignableTo<DurabilityRuntimeActivationResult>();
    }

    [Fact]
    public void Constructor_WhenLeaseIsSupplied_DoesNotActivateAnySelectedService()
    {
        var activated = new DurabilityRuntimeActivated(DurabilityTestData.RuntimeLease());

        activated.Lease.Context.ShouldBe(DurabilityTestData.Context());
    }
}
