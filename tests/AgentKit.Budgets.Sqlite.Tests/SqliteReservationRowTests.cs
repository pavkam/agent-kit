// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies SqliteReservationRow behavior and contracts.</summary>
public sealed class SqliteReservationRowTests
{
    /// <summary>Verifies a valid construction exposes exactly its captured evidence.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var receipt = CreateReceipt();
        var revision = new BudgetAccountingRevision(1);
        var commit = new BudgetCommitResult(receipt.Reservation.Id, 1m, 1m, 0m, 0m);
        var row = new SqliteReservationRow(receipt, BudgetAggregationKind.Sum, DateTimeOffset.UnixEpoch, 2, false, null, commit, commit, revision, 3);
        row.Receipt.ShouldBeSameAs(receipt);
        row.Aggregation.ShouldBe(BudgetAggregationKind.Sum);
        row.StartedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        row.StartRevision.ShouldBe(2);
        row.Released.ShouldBeFalse();
        row.StartExpiration.ShouldBeNull();
        row.OriginalCommit.ShouldBeSameAs(commit);
        row.CurrentCommit.ShouldBeSameAs(commit);
        row.AccountingRevision.ShouldBe(revision);
        row.LatestCorrectionRevision.ShouldBe(3);
        row.IsCapacityRetaining.ShouldBeFalse();
    }

    /// <summary>Verifies a row with no settlement still retains admission capacity.</summary>
    [Fact]
    public void IsCapacityRetaining_WhenUnsettledAndUnreleased_IsTrue()
    {
        var receipt = CreateReceipt();
        var row = new SqliteReservationRow(receipt, BudgetAggregationKind.Sum, null, 0, false, null, null, null, null, 0);
        row.IsCapacityRetaining.ShouldBeTrue();
        row.AccountingRevision.ShouldBeNull();
    }

    /// <summary>Verifies an undefined aggregation is rejected.</summary>
    [Fact]
    public void Constructor_WhenAggregationIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var receipt = CreateReceipt();
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteReservationRow(
            receipt, (BudgetAggregationKind) 99, null, 0, false, null, null, null, null, 0)).ParamName.ShouldBe("aggregation");
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured field.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var original = new SqliteReservationRow(CreateReceipt(), BudgetAggregationKind.Sum, null, 0, false, null, null, null, null, 0);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }

    private static BudgetLedgerReservationReceipt CreateReceipt()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.Parse("20000000-0000-0000-0000-000000000001")), address);
        var request = new BudgetReservationRequest(scope.Id, new BudgetDimension("tokens"), 1, new BudgetUnit("count"), new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000001")), null, new IdempotencyKey("row-test"));
        return new BudgetLedgerReservationReceipt(new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.Parse("40000000-0000-0000-0000-000000000001"))), request, new BudgetEffectiveReservation(DateTimeOffset.UnixEpoch.AddMinutes(5)));
    }
}
