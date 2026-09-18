// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies JsonBudgetLedger behavior and contracts, including the durability the shared suite cannot observe.</summary>
public sealed class JsonBudgetLedgerTests: BudgetLedgerConformanceTests<JsonBudgetLedgerConformanceFixture>
{
    private static readonly BudgetDimension _dimension = new("test.sum");
    private static readonly BudgetUnit _unit = new("count");
    private static readonly AgentId _agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly OperationId _operation = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));

    /// <summary>Verifies a started reservation with unknown spend survives disposal and reappears from a recovery scan.</summary>
    [Fact]
    public async Task InitializeAsync_WhenReopenedAfterDisposal_RetainsStartedUnresolvedReservation()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerScopeReference scope;
        BudgetLedgerReservationReference reservation;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            scope = await CreateScopeAsync(ledger, "durable-scope", 10, token);
            reservation = await ReserveAsync(ledger, scope, "durable-item", 4, token);
            _ = (await ledger.MarkStartedAsync(reservation, token)).ShouldBeOfType<BudgetStarted>();
        }

        using var reopened = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends);

        var page = await reopened.ReadUnresolvedStartedAsync(
            new BudgetUnresolvedReservationQuery(scope, 10, null), token);
        page.Items.Select(item => item.Receipt.Reservation).ShouldBe([reservation]);
        (await reopened.GetSnapshotAsync(scope, token)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(4);
        _ = (await reopened.ReleaseUnstartedAsync(reservation, token)).ShouldBeOfType<BudgetLedgerRetainedStarted>();
    }

    /// <summary>Verifies an indivisible batch replays to its original receipts after the ledger is reopened.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenReplayedAfterReopen_ReturnsTheOriginalBatchReceipts()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerBatchReserveRequest request;
        BudgetLedgerBatchReserveResult original;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            var scope = await CreateScopeAsync(ledger, "replay-scope", 10, token);
            request = new BudgetLedgerBatchReserveRequest(
                scope, [Reservation(scope, "replay-a", 2), Reservation(scope, "replay-b", 3)]);
            original = await ledger.ReserveBatchAsync(request, token);
            _ = original.ShouldBeOfType<BudgetLedgerBatchReserved>();
        }

        using var reopened = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends);

        (await reopened.ReserveBatchAsync(request, token)).ShouldBe(original);
        (await reopened.GetSnapshotAsync(request.Scope, token)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(5);
    }

    /// <summary>Verifies an append that never completed is refused under exact validation and discarded under recovery.</summary>
    [Fact]
    public async Task InitializeAsync_WhenJournalEndsWithTornAppend_RefusesExactlyOrDiscardsThatRecord()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerScopeReference scope;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            scope = await CreateScopeAsync(ledger, "torn-scope", 10, token);
            _ = await ReserveAsync(ledger, scope, "torn-item", 4, token);
        }

        await File.AppendAllTextAsync(
            Path.Combine(root, "ledger.jsonl"),
            "{\"kind\":\"Settled\",\"reservationId\":\"20000000-0000-0000-0000-000000000001\"",
            token);

        var refused = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));
        refused.AcknowledgementUnknown.ShouldBeFalse();
        using var recovered = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends);
        (await recovered.GetSnapshotAsync(scope, token)).Usages.Single().Reserved.ToDecimalChecked().ShouldBe(4);
    }

    /// <summary>Verifies every journaled transition kind replays into byte-identical accounting, revisions, and replay receipts.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEveryTransitionKindIsReplayed_RestoresIdenticalAccountingAndReceipts()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerSettlementRequest settlement;
        BudgetLedgerScopeReference scope;
        BudgetLedgerReservationReference retained;
        BudgetLedgerReservationReference released;
        BudgetLedgerReservationReference expiring;
        BudgetLedgerCorrectionRequest correctionRequest;
        BudgetLedgerReconciliationRequest reconciliationRequest;
        BudgetOverrunHoldResolutionRequest resolutionRequest;
        BudgetCommitResult commit;
        BudgetCorrectionResult correction;
        BudgetLedgerReconciliationResult reconciliation;
        BudgetOverrunHoldResolutionResult resolution;
        BudgetStartResult expired;
        BudgetSnapshot before;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            scope = await CreateScopeAsync(
                ledger, "recover-scope", 100, token, BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
            var overrunning = await ReserveAsync(ledger, scope, "recover-overrun", 10, token);
            _ = await ledger.MarkStartedAsync(overrunning, token);
            settlement = new BudgetLedgerSettlementRequest(overrunning, 20);
            commit = await ledger.SettleAsync(settlement, token);
            correctionRequest = new BudgetLedgerCorrectionRequest(overrunning, 10, 1);
            correction = await ledger.CorrectAsync(correctionRequest, token);
            resolutionRequest = ResolutionRequest(commit.CreatedOverrunHolds.ShouldHaveSingleItem().Reference, "recover-resolve");
            resolution = await ledger.ResolveOverrunHoldAsync(resolutionRequest, token);
            _ = resolution.ShouldBeOfType<BudgetOverrunHoldResolved>();

            var reconciled = await ReserveAsync(ledger, scope, "recover-reconcile", 5, token);
            _ = await ledger.MarkStartedAsync(reconciled, token);
            reconciliationRequest = new BudgetLedgerReconciliationRequest(
                reconciled, new BudgetActualMeasured(5), new IdempotencyKey("recover-reconcile-key"));
            reconciliation = await ledger.ReconcileAsync(reconciliationRequest, token);

            released = await ReserveAsync(ledger, scope, "recover-release", 3, token);
            _ = (await ledger.ReleaseUnstartedAsync(released, token)).ShouldBeOfType<BudgetLedgerReleased>();

            retained = await ReserveAsync(ledger, scope, "recover-retained", 2, token);
            _ = (await ledger.MarkStartedAsync(retained, token)).ShouldBeOfType<BudgetStarted>();
            _ = await ReserveAsync(ledger, scope, "recover-swept", 7, token, DateTimeOffset.UtcNow.AddSeconds(1));
            expiring = await ReserveAsync(ledger, scope, "recover-expired", 4, token, DateTimeOffset.UtcNow.AddSeconds(1));

            // The overrun settled here leaves one active hold, so it must come after every reservation this case
            // needs: an active generation at the boundary refuses further reservations on the same dimension.
            var stillHeld = await ReserveAsync(ledger, scope, "recover-active-hold", 6, token);
            _ = await ledger.MarkStartedAsync(stillHeld, token);
            _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(stillHeld, 12), token);
            fixture.Advance(TimeSpan.FromMinutes(10));
            expired = (await ledger.MarkStartedAsync(expiring, token)).ShouldBeOfType<BudgetStartExpired>();
            before = await ledger.GetSnapshotAsync(scope, token);
        }

        using var reopened = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends);

        var after = await reopened.GetSnapshotAsync(scope, token);
        after.Usages.ShouldBe(before.Usages);
        after.ActiveOverrunHolds.ShouldBe(before.ActiveOverrunHolds);
        after.ActiveOverrunHolds.ShouldHaveSingleItem().Policy.ShouldBe(BudgetOverrunHoldPolicy.RequireAuthorizedResolution);
        (await reopened.SettleAsync(settlement, token)).ShouldBe(commit);
        (await reopened.CorrectAsync(correctionRequest, token)).ShouldBe(correction);
        (await reopened.ReconcileAsync(reconciliationRequest, token)).ShouldBe(reconciliation);
        (await reopened.ResolveOverrunHoldAsync(resolutionRequest, token)).ShouldBe(resolution);
        (await reopened.MarkStartedAsync(expiring, token)).ShouldBe(expired);
        _ = (await reopened.ReleaseUnstartedAsync(released, token)).ShouldBeOfType<BudgetLedgerReleased>();
        var unresolved = await reopened.ReadUnresolvedStartedAsync(
            new BudgetUnresolvedReservationQuery(scope, 10, null), token);
        unresolved.Items.Select(item => item.Receipt.Reservation).ShouldBe([retained]);
    }

    /// <summary>Verifies a disposed ledger refuses further operations instead of serving a dropped projection.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenLedgerIsDisposed_ThrowsObjectDisposedException()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var ledger = fixture.Open(
            root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.RecoverTornAppends);
        var scope = await CreateScopeAsync(ledger, "disposed-scope", 10, token);
        ledger.Dispose();
        ledger.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await ledger.GetSnapshotAsync(scope, token));
    }

    private static async Task<BudgetLedgerScopeReference> CreateScopeAsync(
        JsonBudgetLedger ledger,
        string key,
        decimal limit,
        CancellationToken cancellationToken,
        BudgetOverrunHoldPolicy policy = BudgetOverrunHoldPolicy.ClearWhenReconciled)
    {
        var request = new BudgetScopeRequest(
            null,
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null),
            [new BudgetLimit(_dimension, limit, _unit, BudgetLimitKind.Hard)],
            new IdempotencyKey(key));
        var result = await ledger.CreateScopeAsync(
            new BudgetLedgerScopeCreateRequest(
                request, new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), policy)),
            cancellationToken);
        return result.ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
    }

    private static BudgetReservationRequest Reservation(
        BudgetLedgerScopeReference scope, string key, decimal amount, DateTimeOffset? expiresAt = null) =>
        new(scope.Id, _dimension, amount, _unit, _operation, expiresAt, new IdempotencyKey(key));

    private static async Task<BudgetLedgerReservationReference> ReserveAsync(
        JsonBudgetLedger ledger,
        BudgetLedgerScopeReference scope,
        string key,
        decimal amount,
        CancellationToken cancellationToken,
        DateTimeOffset? expiresAt = null) =>
        (await ledger.ReserveBatchAsync(
            new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, key, amount, expiresAt)]), cancellationToken))
            .ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;

    private static BudgetOverrunHoldResolutionRequest ResolutionRequest(BudgetOverrunHoldReference hold, string key)
    {
        var securityScope = new SecurityAuthorizationScope(
            _agent, null, new BeforeRunOperationCorrelation(_operation, null));
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("operator"), ExecutionSubjectKind.Human);
        var enforcement = new SecurityEnforcementRequest(
            securityScope,
            identity,
            new ComponentId("budget-operator"),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            [BudgetOverrunSecurityBinding.Resource(hold)],
            BudgetOverrunSecurityBinding.Fingerprint(hold),
            new SecurityRevocationVersion(1));
        var receipt = new SecurityEnforcementIntentReceipt(
            new SecurityEnforcementIntentId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
            new GrantId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            new SecurityRequestId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
            enforcement,
            null,
            new ContentHash($"sha256:{key}"),
            DateTimeOffset.UnixEpoch);
        return new BudgetOverrunHoldResolutionRequest(hold, receipt, new IdempotencyKey(key));
    }
}
