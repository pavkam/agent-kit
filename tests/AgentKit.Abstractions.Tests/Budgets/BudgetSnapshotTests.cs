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
}
