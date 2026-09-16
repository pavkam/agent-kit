// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Planning;

using AgentKit;

using Shouldly;

/// <summary>Verifies PlanSecurityBinding behavior and contracts.</summary>
public sealed class PlanSecurityBindingTests
{
    [Fact]
    public void ReplaceFingerprint_WhenOrderedItemStateChanges_ChangesEvidence()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        ImmutableArray<WorkPlanItem> original = [new(new PlanItemId("one"), "First.", PlanItemStatus.Pending)];
        ImmutableArray<WorkPlanItem> changed = [new(new PlanItemId("one"), "First.", PlanItemStatus.Completed)];
        var first = PlanSecurityBinding.ReplaceFingerprint(address, "Plan", original, null);
        var second = PlanSecurityBinding.ReplaceFingerprint(address, "Plan", changed, null);
        second.ShouldNotBe(first);
    }

    [Fact]
    public void Resource_WhenCalled_ProducesCanonicalApplicationStateResource()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var resource = PlanSecurityBinding.Resource(address);
        resource.Kind.ShouldBe(ProtectedResourceKind.ApplicationState);
        resource.Identifier.ShouldBe($"session:{address.AgentId}/{address.SessionId}/plan");
    }

    [Fact]
    public void Resource_WhenAddressIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => PlanSecurityBinding.Resource(null!));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void ReadFingerprint_WhenAddressIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => PlanSecurityBinding.ReadFingerprint(null!));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void ReadFingerprint_WhenAddressesAreEquivalent_IsStable()
    {
        var address = new SessionAddress(new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")));
        var other = new SessionAddress(address.AgentId, address.SessionId);
        PlanSecurityBinding.ReadFingerprint(address).ShouldBe(PlanSecurityBinding.ReadFingerprint(other));
    }

    [Fact]
    public void ReplaceFingerprint_WhenAddressIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => PlanSecurityBinding.ReplaceFingerprint(null!, "Plan", [], null));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void ReplaceFingerprint_WhenTitleIsBlank_ThrowsExactParameter()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var exception = Should.Throw<ArgumentException>(() => PlanSecurityBinding.ReplaceFingerprint(address, " ", [], null));
        exception.ParamName.ShouldBe("title");
    }

    [Fact]
    public void ReplaceFingerprint_WhenItemsContainNull_ThrowsExactParameter()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var exception = Should.Throw<ArgumentException>(() => PlanSecurityBinding.ReplaceFingerprint(address, "Plan", [null!], null));
        exception.ParamName.ShouldBe("items");
    }

    [Fact]
    public void StatusFingerprint_WhenAddressIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => PlanSecurityBinding.StatusFingerprint(null!, new PlanItemId("one"), PlanItemStatus.Completed, new PlanRevision(1)));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void StatusFingerprint_WhenStatusIsUndefined_ThrowsExactParameter()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => PlanSecurityBinding.StatusFingerprint(address, new PlanItemId("one"), (PlanItemStatus) 999, new PlanRevision(1)));
        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void StatusFingerprint_WhenInputsAreEquivalent_IsStable()
    {
        var address = new SessionAddress(new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")));
        var itemId = new PlanItemId("one");
        PlanSecurityBinding.StatusFingerprint(address, itemId, PlanItemStatus.Completed, new PlanRevision(1))
            .ShouldBe(PlanSecurityBinding.StatusFingerprint(address, itemId, PlanItemStatus.Completed, new PlanRevision(1)));
    }
}
