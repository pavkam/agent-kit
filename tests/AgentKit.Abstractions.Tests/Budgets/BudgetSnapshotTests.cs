// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;
/// <summary>Verifies BudgetSnapshot behavior and contracts.</summary>
public sealed class BudgetSnapshotTests
{
    [Fact]
    public void BudgetSnapshot_WhenActiveHoldArrayIsDefault_ThrowsExactParameterName() => Should.Throw<ArgumentException>(() => new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, [], default)).ParamName.ShouldBe("activeOverrunHolds");

    [Fact]
    public void OverrunResultArrays_WhenDefaultOrContainingNull_ThrowExactParameterName() => Should.Throw<ArgumentException>(() => new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, [], [null!])).ParamName.ShouldBe("activeOverrunHolds");

    [Fact]
    public void OverrunResultArrays_WhenEmptyIsAllowed_PreserveEmptyEvidence() => new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, [], []).ActiveOverrunHolds.ShouldBeEmpty();

    [Fact]
    public void Constructor_WhenUsagesIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, default));
        exception.ParamName.ShouldBe("usages");
    }

    [Fact]
    public void Constructor_WhenThreeArgumentOverloadIsUsed_DefaultsActiveOverrunHoldsToEmpty()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        ImmutableArray<BudgetDimensionUsage> usages = [Usage()];
        var snapshot = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, usages);
        snapshot.ScopeId.ShouldBe(scopeId);
        snapshot.ObservedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        snapshot.Usages.ShouldBe(usages);
        snapshot.ActiveOverrunHolds.ShouldBeEmpty();
    }

    [Fact]
    public void Equals_WhenAllFieldsMatch_InstancesAreEqual()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        ImmutableArray<BudgetDimensionUsage> usages = [Usage()];
        var first = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, usages);
        var second = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, usages);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenUsagesDiffer_IsNotEqual()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var first = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, [Usage()]);
        var second = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, []);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetSnapshot(new BudgetScopeId(Guid.NewGuid()), DateTimeOffset.UnixEpoch, [Usage()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Equals_WhenActiveOverrunHoldsMatch_HasEqualHashCode()
    {
        var scopeId = new BudgetScopeId(Guid.NewGuid());
        var hold = Hold();
        var first = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, [], [hold]);
        var second = new BudgetSnapshot(scopeId, DateTimeOffset.UnixEpoch, [], [hold]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private static BudgetDimensionUsage Usage() => new(new BudgetDimension("tests.requests"), new BudgetUnit("requests"), 1m, 0m, null);

    private static BudgetOverrunHold Hold()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("00000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("00000000-0000-0000-0000-000000000002")), address);
        var reference = new BudgetOverrunHoldReference(scope, new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.Parse("00000000-0000-0000-0000-000000000003"))), new BudgetAccountingRevision(1));
        return new BudgetOverrunHold(reference, new BudgetDimension("tests.overrun"), new BudgetUnit("count"), 1, 2, BudgetOverrunHoldPolicy.ClearWhenReconciled);
    }
}
