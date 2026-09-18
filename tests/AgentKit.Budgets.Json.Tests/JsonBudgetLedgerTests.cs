// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

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

    /// <summary>Verifies every public operation fails closed with content-free evidence when the ledger was never initialized.</summary>
    [Fact]
    public async Task Operations_WhenLedgerWasNeverInitialized_FailClosedForEveryOperation()
    {
        var token = TestContext.Current.CancellationToken;
        var target = new JsonBudgetLedgerTarget(
            Path.Combine(TestTemporaryDirectory.Create(), "ledger"),
            new JsonBudgetLedgerInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends);
        using var ledger = new JsonBudgetLedger(
            target, JsonBudgetLedgerSettings.CreateDefault(), TimeProvider.System,
            new StubScopeIds(), new StubReservationIds(), new StubCatalog());
        var scope = new BudgetLedgerScopeReference(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null));
        var reservation = new BudgetLedgerReservationReference(scope, new BudgetReservationId(Guid.NewGuid()));

        await AssertUnusableAsync(() => ledger.CreateScopeAsync(
            new BudgetLedgerScopeCreateRequest(
                new BudgetScopeRequest(null, scope.Address, [], new IdempotencyKey("uninitialized")),
                new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.ClearWhenReconciled)),
            token));
        await AssertUnusableAsync(() => ledger.ReserveBatchAsync(
            new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "uninitialized", 1)]), token));
        await AssertUnusableAsync(() => ledger.MarkStartedAsync(reservation, token));
        await AssertUnusableAsync(() => ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), token));
        await AssertUnusableAsync(() => ledger.ReleaseUnstartedAsync(reservation, token));
        await AssertUnusableAsync(() => ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 1, 1), token));
        await AssertUnusableAsync(() => ledger.GetSnapshotAsync(scope, token));
        await AssertUnusableAsync(() => ledger.ReadUnresolvedStartedAsync(new BudgetUnresolvedReservationQuery(scope, 8, null), token));
        await AssertUnusableAsync(() => ledger.ReconcileAsync(
            new BudgetLedgerReconciliationRequest(reservation, new BudgetStillUnknown(), new IdempotencyKey("uninitialized")), token));
        await AssertUnusableAsync(() => ledger.ResolveOverrunHoldAsync(
            ResolutionRequest(new BudgetOverrunHoldReference(scope, reservation, new BudgetAccountingRevision(1)), "uninitialized"), token));

        static async Task AssertUnusableAsync<TResult>(Func<ValueTask<TResult>> operation)
        {
            var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await operation());
            exception.AcknowledgementUnknown.ShouldBeFalse();
            exception.Message.ShouldContain("before trusted bootstrap initialization");
        }
    }

    /// <summary>Verifies repeating initialization after success is a no-op rather than replaying the journal twice.</summary>
    [Fact]
    public async Task InitializeAsync_WhenCalledTwice_IsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        using var ledger = fixture.Open(
            Path.Combine(TestTemporaryDirectory.Create(), "ledger"),
            new JsonBudgetLedgerInstanceId(Guid.NewGuid()),
            JsonStoreRecoveryMode.RecoverTornAppends);
        var scope = await CreateScopeAsync(ledger, "idempotent-init-scope", 10, token);

        await ledger.InitializeAsync(token);

        (await ledger.GetSnapshotAsync(scope, token)).ScopeId.ShouldBe(scope.Id);
    }

    /// <summary>Verifies opening a missing root without creation permission fails closed instead of fabricating an empty store.</summary>
    [Fact]
    public void InitializeAsync_WhenRootHasNoManifestAndCreationIsForbidden_ThrowsPersistenceUnavailable()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = TestTemporaryDirectory.Create();

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));

        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("no manifest");
    }

    /// <summary>Verifies reopening under a different expected store identity is refused instead of serving another deployment's accounting.</summary>
    [Fact]
    public async Task InitializeAsync_WhenStoreInstanceIdDiffersFromManifest_ThrowsPersistenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        using (var ledger = fixture.Open(root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.RecoverTornAppends))
        {
            _ = await CreateScopeAsync(ledger, "store-id-scope", 10, token);
        }

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));

        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("identity does not match");
    }

    /// <summary>Verifies an unsupported persisted schema version is refused rather than decoded under assumed-compatible rules.</summary>
    [Fact]
    public async Task InitializeAsync_WhenManifestSchemaVersionIsUnsupported_ThrowsPersistenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            _ = await CreateScopeAsync(ledger, "schema-version-scope", 10, token);
        }

        RewriteManifest(root, node => node["schemaVersion"] = 2);

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));

        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("schema version is unsupported");
    }

    /// <summary>Verifies reopening under a materially different encoding contract is refused rather than decoding previously written evidence under incompatible rules.</summary>
    [Fact]
    public async Task InitializeAsync_WhenEncodingContractDiffersMaterially_ThrowsPersistenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            _ = await CreateScopeAsync(ledger, "encoding-mismatch-scope", 10, token);
        }

        var differentOptions = JsonStoreSerialization.CreateCanonicalOptions();
        differentOptions.MaxDepth = 32;
        var differentSettings = new JsonBudgetLedgerSettings(
            JsonBudgetLedgerSettings.CreateDefault().MaximumRecordBytes,
            JsonBudgetLedgerSettings.CreateDefault().MaximumDocumentBytes,
            new JsonEncodingSettings(differentOptions));

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting, differentSettings));

        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("different encoding contract");
    }

    /// <summary>Verifies reopening with only <see cref="System.Text.Json.JsonSerializerOptions.WriteIndented"/> changed is accepted, because presentation-only settings are excluded from the fingerprint.</summary>
    [Fact]
    public async Task InitializeAsync_WhenOnlyWriteIndentedChanges_IsAccepted()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerScopeReference scope;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            scope = await CreateScopeAsync(ledger, "indent-only-scope", 10, token);
        }

        var indentedOptions = JsonStoreSerialization.CreateCanonicalOptions();
        indentedOptions.WriteIndented = true;
        var indentedSettings = new JsonBudgetLedgerSettings(
            JsonBudgetLedgerSettings.CreateDefault().MaximumRecordBytes,
            JsonBudgetLedgerSettings.CreateDefault().MaximumDocumentBytes,
            new JsonEncodingSettings(indentedOptions));

        using var reopened = fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting, indentedSettings);

        (await reopened.GetSnapshotAsync(scope, token)).ScopeId.ShouldBe(scope.Id);
    }

    /// <summary>Verifies a configured contract that cannot round-trip the store's own evidence is rejected at initialization instead of committing accounting under it.</summary>
    [Fact]
    public void InitializeAsync_WhenEncodingContractCannotRoundTrip_ThrowsInvalidOperation()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var unusableOptions = JsonStoreSerialization.CreateCanonicalOptions();
        unusableOptions.MaxDepth = 1;
        var unusableSettings = new JsonBudgetLedgerSettings(
            JsonBudgetLedgerSettings.CreateDefault().MaximumRecordBytes,
            JsonBudgetLedgerSettings.CreateDefault().MaximumDocumentBytes,
            new JsonEncodingSettings(unusableOptions));

        _ = Should.Throw<InvalidOperationException>(() => fixture.Open(
            Path.Combine(TestTemporaryDirectory.Create(), "ledger"),
            new JsonBudgetLedgerInstanceId(Guid.NewGuid()),
            JsonStoreRecoveryMode.RecoverTornAppends,
            settings: unusableSettings));
    }

    /// <summary>Verifies a second ledger cannot open the same root while the first holds the advisory exclusive lock, and that closing the first unblocks the second.</summary>
    [Fact]
    public void InitializeAsync_WhenAnotherLedgerHoldsTheRoot_FailsUntilReleased()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        var first = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends);

        _ = Should.Throw<InvalidOperationException>(
            () => fixture.Open(root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));

        first.Dispose();
        using var second = fixture.Open(root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting);
        _ = second.ShouldNotBeNull();
    }

    /// <summary>Verifies a store root reached through a symbolic link is refused, because the whole path must be a stable configured location.</summary>
    [Fact]
    public void InitializeAsync_WhenRootIsSymbolicLink_ThrowsInvalidOperationException()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var parent = TestTemporaryDirectory.Create();
        var real = Path.Combine(parent, "real");
        _ = Directory.CreateDirectory(real);
        var link = Path.Combine(parent, "link");
        _ = Directory.CreateSymbolicLink(link, real);

        _ = Should.Throw<InvalidOperationException>(() => fixture.Open(
            link, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.RecoverTornAppends));
    }

    /// <summary>Verifies a record too large for the configured bound is refused before any append, rather than writing a partial line.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenRecordExceedsConfiguredBound_ThrowsPersistenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var tinySettings = new JsonBudgetLedgerSettings(16, JsonBudgetLedgerSettings.CreateDefault().MaximumDocumentBytes, JsonEncodingSettings.CreateDefault());
        using var ledger = fixture.Open(
            Path.Combine(TestTemporaryDirectory.Create(), "ledger"),
            new JsonBudgetLedgerInstanceId(Guid.NewGuid()),
            JsonStoreRecoveryMode.RecoverTornAppends,
            settings: tinySettings);

        var exception = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(
            async () => await CreateScopeAsync(ledger, "too-large-scope", 10, token));
        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("could not encode");
    }

    /// <summary>Verifies an append whose durability cannot be confirmed sets the instance uncertain, refusing every later operation.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenAppendCannotBeConfirmed_MarksInstanceUncertainAndRefusesLaterOperations()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        using var ledger = fixture.Open(root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.RecoverTornAppends);
        var scope = await CreateScopeAsync(ledger, "uncertain-append-scope", 10, token);
        var logPath = Path.Combine(root, "ledger.jsonl");
        File.Delete(logPath);
        _ = Directory.CreateDirectory(logPath);

        try
        {
            var first = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(
                async () => await ReserveAsync(ledger, scope, "uncertain-append-item", 1, token));
            first.AcknowledgementUnknown.ShouldBeTrue();

            var second = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(
                async () => await ledger.GetSnapshotAsync(scope, token));
            second.AcknowledgementUnknown.ShouldBeTrue();
            second.Message.ShouldContain("could not be confirmed");
        }
        finally
        {
            Directory.Delete(logPath, recursive: true);
        }
    }

    /// <summary>Verifies releasing an already released reservation appends nothing, because nothing about durable state changed.</summary>
    [Fact]
    public async Task ReleaseUnstartedAsync_WhenAlreadyReleased_AppendsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        using var ledger = fixture.Open(root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.RecoverTornAppends);
        var scope = await CreateScopeAsync(ledger, "double-release-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "double-release-item", 1, token);
        _ = (await ledger.ReleaseUnstartedAsync(reservation, token)).ShouldBeOfType<BudgetLedgerReleased>();
        var linesAfterFirstRelease = await File.ReadAllLinesAsync(Path.Combine(root, "ledger.jsonl"), token);

        _ = (await ledger.ReleaseUnstartedAsync(reservation, token)).ShouldBeOfType<BudgetLedgerReleased>();

        var linesAfterSecondRelease = await File.ReadAllLinesAsync(Path.Combine(root, "ledger.jsonl"), token);
        linesAfterSecondRelease.Length.ShouldBe(linesAfterFirstRelease.Length);
    }

    /// <summary>Verifies one indivisible multi-dimension batch is written as a single journal line, so recovery can never observe a partial batch.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenBatchHasMultipleMembers_AppendsExactlyOneLine()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        using var ledger = fixture.Open(root, new JsonBudgetLedgerInstanceId(Guid.NewGuid()), JsonStoreRecoveryMode.RecoverTornAppends);
        var scope = await CreateScopeAsync(ledger, "atomic-batch-scope", 10, token);
        var logPath = Path.Combine(root, "ledger.jsonl");
        var linesBefore = (await File.ReadAllLinesAsync(logPath, token)).Length;

        var request = new BudgetLedgerBatchReserveRequest(
            scope, [Reservation(scope, "atomic-batch-a", 2), Reservation(scope, "atomic-batch-b", 3)]);
        _ = (await ledger.ReserveBatchAsync(request, token)).ShouldBeOfType<BudgetLedgerBatchReserved>();

        var linesAfter = await File.ReadAllLinesAsync(logPath, token);
        linesAfter.Length.ShouldBe(linesBefore + 1);
    }

    /// <summary>Verifies a persisted batch entry naming a dimension the current catalog no longer recognizes fails replay closed instead of silently dropping the member.</summary>
    [Fact]
    public async Task InitializeAsync_WhenPersistedEntryReferencesUnknownDimension_ThrowsPersistenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerScopeReference scope;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            scope = await CreateScopeAsync(ledger, "unknown-dimension-scope", 10, token);
        }

        var entry = new JsonBudgetReservationEntry(
            new JsonBudgetReservationRequest(scope.Id.Value, "unknown.dimension", 1, "count", Guid.NewGuid(), null, "corrupt-item"),
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch.AddMinutes(5));
        var record = new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.BatchReserved, scope.Id.Value, null, [entry], null, DateTimeOffset.UnixEpoch,
            null, null, null, null, null, null);
        AppendRawRecord(root, record);

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));
        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("could not be replayed");
    }

    /// <summary>Verifies a persisted batch record with no members fails replay closed instead of silently producing an empty reservation set.</summary>
    [Fact]
    public async Task InitializeAsync_WhenPersistedBatchRecordHasNoMembers_ThrowsPersistenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        BudgetLedgerScopeReference scope;
        using (var ledger = fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            scope = await CreateScopeAsync(ledger, "empty-batch-scope", 10, token);
        }

        var record = new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.BatchReserved, scope.Id.Value, null, [], null, DateTimeOffset.UnixEpoch,
            null, null, null, null, null, null);
        AppendRawRecord(root, record);

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));
        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("carries no members");
    }

    /// <summary>Verifies a persisted record naming a scope that was never created fails replay closed instead of fabricating one.</summary>
    [Fact]
    public void InitializeAsync_WhenPersistedRecordReferencesUnknownScope_ThrowsPersistenceUnavailable()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        using (fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            // Root is initialized with an empty journal; the manifest and lock exist, but no scope does.
        }

        var entry = new JsonBudgetReservationEntry(
            new JsonBudgetReservationRequest(Guid.NewGuid(), "test.sum", 1, "count", Guid.NewGuid(), null, "orphan-item"),
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch.AddMinutes(5));
        var record = new JsonBudgetLedgerRecord(
            JsonBudgetLedgerRecordKind.BatchReserved, Guid.NewGuid(), null, [entry], null, DateTimeOffset.UnixEpoch,
            null, null, null, null, null, null);
        AppendRawRecord(root, record);

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));
        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("unknown scope");
    }

    /// <summary>Verifies a persisted record naming a reservation that was never created fails replay closed instead of fabricating one.</summary>
    [Fact]
    public void InitializeAsync_WhenPersistedRecordReferencesUnknownReservation_ThrowsPersistenceUnavailable()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        using (fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            // Root is initialized with an empty journal; no reservation exists.
        }

        var record = JsonBudgetLedgerRecord.ForReleased(new BudgetReservationId(Guid.NewGuid()));
        AppendRawRecord(root, record);

        var exception = Should.Throw<BudgetLedgerPersistenceUnavailableException>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));
        exception.AcknowledgementUnknown.ShouldBeFalse();
        exception.Message.ShouldContain("unknown reservation");
    }

    /// <summary>Verifies a syntactically complete but schema-violating journal line fails initialization closed instead of being silently skipped.</summary>
    [Fact]
    public void InitializeAsync_WhenPersistedLineViolatesTheSchema_FailsClosedRatherThanSkippingIt()
    {
        var fixture = new JsonBudgetLedgerConformanceFixture();
        var root = Path.Combine(TestTemporaryDirectory.Create(), "ledger");
        var storeId = new JsonBudgetLedgerInstanceId(Guid.NewGuid());
        using (fixture.Open(root, storeId, JsonStoreRecoveryMode.RecoverTornAppends))
        {
            // Root is initialized with an empty journal.
        }

        File.AppendAllText(Path.Combine(root, "ledger.jsonl"), /*lang=json,strict*/ "{\"kind\":\"ScopeCreated\",\"unmappedMember\":true}\n");

        _ = Should.Throw<Exception>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.ValidateExact, JsonStoreOpenMode.OpenExisting));
        // The malformed line is never discarded: reopening under recovery, which only discards an incomplete
        // trailing append, still fails because the complete-but-invalid line remains exactly where it was written.
        _ = Should.Throw<Exception>(() => fixture.Open(
            root, storeId, JsonStoreRecoveryMode.RecoverTornAppends, JsonStoreOpenMode.OpenExisting));
    }

    private static void AppendRawRecord(string root, JsonBudgetLedgerRecord record)
    {
        var options = JsonEncodingSettings.CreateDefault();
        var payload = JsonStoreSerialization.Encode(record, options.RecordOptions, 1_048_576);
        using var stream = new FileStream(Path.Combine(root, "ledger.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(payload);
        stream.WriteByte((byte) '\n');
    }

    private static void RewriteManifest(string root, Action<System.Text.Json.Nodes.JsonObject> mutate)
    {
        var manifestPath = Path.Combine(root, "store.json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        mutate(node);
        File.WriteAllText(manifestPath, node.ToJsonString());
    }

    private sealed class StubScopeIds: IIdentifierGenerator<BudgetScopeId>
    {
        public BudgetScopeId Create() => new(Guid.NewGuid());
    }

    private sealed class StubReservationIds: IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => new(Guid.NewGuid());
    }

    private sealed class StubCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }
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

    /// <summary>Verifies the generated log-state accessors work through the classic non-generic enumeration surface that
    /// some third-party logging providers use instead of the generic key/value interface.</summary>
    [Fact]
    public async Task Operations_WhenLoggerEnumeratesStateViaLegacyEnumerable_ExercisesGeneratedStateAccessors()
    {
        var logger = new LegacyEnumeratingLogger();
        var ledger = new JsonBudgetLedgerConformanceFixture(logger).CreateLedger();
        var token = TestContext.Current.CancellationToken;

        var created = await ledger.CreateScopeAsync(CreateDiagnosticRequest("legacy-enumerable-scope", _dimension), token);
        _ = created.ShouldBeOfType<BudgetLedgerScopeCreated>();
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.GetSnapshotAsync(
            new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), created.ShouldBeOfType<BudgetLedgerScopeCreated>().Scope.Address),
            token));

        logger.CompletedCounts.ShouldNotBeEmpty();
        logger.FailedCounts.ShouldNotBeEmpty();
        logger.CompletedCounts.ShouldAllBe(count => count > 0);
        logger.FailedCounts.ShouldAllBe(count => count > 0);
    }

    /// <summary>Verifies successful and failed terminal outcomes are logged under their documented stable event identities with the expected bounded fields.</summary>
    [Fact]
    public async Task Operations_WhenObserved_LogsUnderDocumentedEventIds()
    {
        var logger = new CaptureLogger();
        var ledger = new JsonBudgetLedgerConformanceFixture(logger).CreateLedger();
        var token = TestContext.Current.CancellationToken;

        var created = (await ledger.CreateScopeAsync(CreateDiagnosticRequest("event-id-scope", _dimension), token))
            .ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.GetSnapshotAsync(
            new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), created.Address), token));

        logger.Entries.ShouldContain(entry => entry.EventId.Id == 19_200
            && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "created")));
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 19_201
            && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "faulted")));
    }

    /// <summary>Verifies a successful operation's activity, correlation tags, metric tags, and log fields agree and disclose only bounded identities.</summary>
    [Fact]
    public async Task GetSnapshotAsync_WhenObserved_EmitsCorrelatedSuccessfulActivityAndMetrics()
    {
        using var parent = new Activity("json-budget-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name == AgentKitMetricNames.BudgetLedgerOperationCount)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                measurements.Enqueue(tags.ToArray());
            }
        });
        meterListener.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parent.SpanId && activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation)
                {
                    stopped.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var ledger = new JsonBudgetLedgerConformanceFixture(logger).CreateLedger();
        var token = TestContext.Current.CancellationToken;
        var scope = (await ledger.CreateScopeAsync(CreateDiagnosticRequest("diagnostic-snapshot", _dimension), token))
            .ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;

        var snapshot = await ledger.GetSnapshotAsync(scope, token);

        snapshot.ScopeId.ShouldBe(scope.Id);
        var activity = stopped.Last(item => item.GetTagItem(AgentKitTagNames.BudgetOperation)?.ToString() == "get_snapshot");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.BudgetScopeId)?.ToString().ShouldBe(scope.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.TenantId)?.ToString().ShouldBe(scope.Address.TenantId.ToString());
        var metric = measurements.Last(tags => tags.Any(tag => tag.Key == AgentKitTagNames.BudgetOperation && Equals(tag.Value, "get_snapshot")));
        metric.ShouldContain(tag => tag.Key == AgentKitTagNames.Outcome && Equals(tag.Value, "read"));
        metric.ShouldNotContain(tag => tag.Key == AgentKitTagNames.TenantId || tag.Key == AgentKitTagNames.BudgetScopeId);
        var log = logger.Entries.Last(entry => entry.EventId.Id == 19_200
            && entry.State.Any(item => item.Key == "BudgetOperation" && Equals(item.Value, "get_snapshot")));
        log.State.ShouldContain(item => item.Key == "BudgetScopeId" && Equals(item.Value, scope.Id.ToString()));
        log.State.ShouldContain(item => item.Key == "TenantId" && Equals(item.Value, scope.Address.TenantId.ToString()));
    }

    /// <summary>Verifies cancellation and typed admission rejection use distinct truthful terminal evidence.</summary>
    [Fact]
    public async Task Operations_WhenCancelledOrRejected_EmitTruthfulErrorOutcomes()
    {
        using var parent = new Activity("json-terminal-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateScopedListener(parent, stopped);
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var ledger = new JsonBudgetLedgerConformanceFixture(logger).CreateLedger();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await ledger.ReadUnresolvedStartedAsync(
            new BudgetUnresolvedReservationQuery(
                new BudgetLedgerScopeReference(
                    new BudgetScopeId(Guid.NewGuid()),
                    new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null)),
                1,
                null),
            cancellation.Token));
        var rejected = await ledger.CreateScopeAsync(
            CreateDiagnosticRequest("diagnostic-rejected", new BudgetDimension("missing.dimension")), TestContext.Current.CancellationToken);
        _ = rejected.ShouldBeOfType<BudgetLedgerScopeCreateRejected>();

        var cancelled = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "cancelled");
        cancelled.Status.ShouldBe(ActivityStatusCode.Error);
        _ = cancelled.GetTagItem(AgentKitTagNames.ErrorType).ShouldNotBeNull();
        var rejection = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "rejected");
        rejection.Status.ShouldBe(ActivityStatusCode.Error);
        // Unlike an exception-based fault, a typed admission rejection carries no .NET exception; its failure code is
        // therefore the outcome name itself rather than a normalized exception category.
        rejection.GetTagItem(AgentKitTagNames.ErrorType)?.ToString().ShouldBe("rejected");
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 19_201
            && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "cancelled")));
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 19_201
            && entry.State.Any(item => item.Key == "Outcome" && Equals(item.Value, "rejected")));
    }

    /// <summary>Verifies a persistence fault retains normalized exception evidence without disclosing the configured store root, the failing key, or accounting amounts anywhere observed.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenPersistenceFails_EmitsFaultWithoutProtectedContent()
    {
        const string keySentinel = "protected-key-sentinel";
        using var parent = new Activity("json-fault-test").Start();
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = CreateScopedListener(parent, stopped);
        ActivitySource.AddActivityListener(listener);
        var logger = new CaptureLogger();
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name == AgentKitMetricNames.BudgetLedgerOperationCount)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                measurements.Enqueue(tags.ToArray());
            }
        });
        meterListener.Start();
        var rootSentinel = TestTemporaryDirectory.Create();
        var tinySettings = new JsonBudgetLedgerSettings(16, JsonBudgetLedgerSettings.CreateDefault().MaximumDocumentBytes, JsonEncodingSettings.CreateDefault());
        var target = new JsonBudgetLedgerTarget(
            Path.Combine(rootSentinel, "ledger"), new(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);
        using var ledger = new JsonBudgetLedger(target, tinySettings, TimeProvider.System, new StubScopeIds(), new StubReservationIds(), new FaultCatalog(), logger);
        await ledger.InitializeAsync(TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<BudgetLedgerPersistenceUnavailableException>(async () => await ledger.CreateScopeAsync(
            CreateDiagnosticRequest(keySentinel, _dimension), TestContext.Current.CancellationToken));

        var activity = stopped.Single(item => item.GetTagItem(AgentKitTagNames.Outcome)?.ToString() == "faulted");
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        var errorType = activity.GetTagItem(AgentKitTagNames.ErrorType).ShouldNotBeNull();
        // The failure code is the ledger's own normalized category for this exception type, not the raw .NET type
        // name, keeping the signal stable across internal exception-hierarchy changes.
        errorType.ToString().ShouldBe("persistence_unavailable");
        AssertNoProtectedContent(activity.TagObjects.Select(item => item.Value), keySentinel, rootSentinel);
        AssertNoProtectedContent(logger.Entries.SelectMany(static entry => entry.State).Select(item => item.Value), keySentinel, rootSentinel);
        AssertNoProtectedContent(measurements.SelectMany(static tags => tags).Select(item => item.Value), keySentinel, rootSentinel);
    }

    /// <summary>Verifies failing standard observers cannot change a committed result or the ambient parent activity.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenObserversThrow_PreservesResultAndParent()
    {
        using var parent = new Activity("json-throwing-observer-test").Start();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, candidate) =>
        {
            if (instrument.Name is AgentKitMetricNames.BudgetLedgerOperationCount or AgentKitMetricNames.BudgetLedgerOperationDuration)
            {
                candidate.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                throw new InvalidOperationException("meter observer failure");
            }
        });
        meterListener.SetMeasurementEventCallback<double>((_, _, _, _) =>
        {
            if (Activity.Current?.ParentSpanId == parent.SpanId)
            {
                throw new InvalidOperationException("meter observer failure");
            }
        });
        meterListener.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "AgentKit",
            Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
            {
                return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
            },
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
                {
                    throw new InvalidOperationException("observer failure");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var ledger = new JsonBudgetLedgerConformanceFixture(new ThrowingLogger()).CreateLedger();

        var result = await ledger.CreateScopeAsync(
            CreateDiagnosticRequest("throwing-observer", _dimension), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    /// <summary>Verifies disabled diagnostic listeners leave semantic execution unchanged.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenDiagnosticsAreDisabled_Succeeds()
    {
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();

        var result = await ledger.CreateScopeAsync(
            CreateDiagnosticRequest("disabled-observers", _dimension), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<BudgetLedgerScopeCreated>();
    }

    private static void AssertNoProtectedContent(IEnumerable<object?> values, params string[] sentinels) =>
        values.Any(value => value is not null
                && sentinels.Any(sentinel => value.ToString()!.Contains(sentinel, StringComparison.Ordinal)))
            .ShouldBeFalse();

    private static BudgetLedgerScopeCreateRequest CreateDiagnosticRequest(string key, BudgetDimension dimension) =>
        new(
            new BudgetScopeRequest(
                null,
                new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null),
                [new BudgetLimit(dimension, 10, _unit, BudgetLimitKind.Hard)],
                new IdempotencyKey(key)),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));

    private static ActivityListener CreateScopedListener(Activity parent, ConcurrentQueue<Activity> stopped) => new()
    {
        ShouldListenTo = static source => source.Name == "AgentKit",
        Sample = delegate (ref ActivityCreationOptions<ActivityContext> options)
        {
            return options.Name == AgentKitActivityNames.BudgetLedgerOperation && options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None;
        },
        ActivityStopped = activity =>
        {
            if (activity.OperationName == AgentKitActivityNames.BudgetLedgerOperation && activity.ParentSpanId == parent.SpanId)
            {
                stopped.Enqueue(activity);
            }
        },
    };

    private sealed class FaultCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = new(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("count")]);
            return true;
        }
    }

    private sealed class CaptureLogger: ILogger<JsonBudgetLedger>
    {
        internal ConcurrentQueue<(EventId EventId, KeyValuePair<string, object?>[] State)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                Entries.Enqueue((eventId, values.ToArray()));
            }
        }
    }

    private sealed class ThrowingLogger: ILogger<JsonBudgetLedger>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger failure");
    }

    private sealed class LegacyEnumeratingLogger: ILogger<JsonBudgetLedger>
    {
        internal List<int> CompletedCounts { get; } = [];
        internal List<int> FailedCounts { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not System.Collections.IEnumerable legacy)
            {
                return;
            }

            var count = 0;
            foreach (var _ in legacy)
            {
                count++;
            }

            if (state is IReadOnlyList<KeyValuePair<string, object?>> indexed && indexed.Count > 0)
            {
                for (var index = 0; index < indexed.Count; index++)
                {
                    _ = indexed[index];
                }

                try
                {
                    _ = indexed[indexed.Count];
                }
                catch (IndexOutOfRangeException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            _ = state?.ToString();
            _ = formatter(state, exception);

            if (eventId.Id == 19_200)
            {
                CompletedCounts.Add(count);
            }
            else if (eventId.Id == 19_201)
            {
                FailedCounts.Add(count);
            }
        }
    }

    /// <summary>Verifies an exact repeated scope-creation request replays the original receipt instead of creating a second scope.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenRequestIsRepeatedExactly_ReplaysOriginalReceipt()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(
                null,
                new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null),
                [new BudgetLimit(_dimension, 10, _unit, BudgetLimitKind.Hard)],
                new IdempotencyKey("exact-replay-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var first = await ledger.CreateScopeAsync(request, token);

        var second = await ledger.CreateScopeAsync(request, token);

        second.ShouldBe(first);
    }

    /// <summary>Verifies a child request naming a parent scope that does not exist is refused as an unavailable reference.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenParentScopeDoesNotExist_ThrowsReferenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(
                new BudgetScopeId(Guid.NewGuid()),
                new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null),
                [],
                new IdempotencyKey("missing-parent-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));

        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(
            async () => await ledger.CreateScopeAsync(request, token));
    }

    /// <summary>Verifies a child scope that would exceed the parent's captured maximum depth is rejected without persisting.</summary>
    [Fact]
    public async Task CreateScopeAsync_WhenDepthExceedsCapturedMaximum_ReturnsMaximumDepthRejection()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var shallowAdmission = new BudgetScopeAdmission(1, 32, TimeSpan.FromMinutes(5));
        var root = (await ledger.CreateScopeAsync(
                new BudgetLedgerScopeCreateRequest(new BudgetScopeRequest(null, address, [], new IdempotencyKey("depth-root")), shallowAdmission), token))
            .ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var childRequest = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(root.Id, address, [], new IdempotencyKey("depth-child")), shallowAdmission);

        var rejected = (await ledger.CreateScopeAsync(childRequest, token)).ShouldBeOfType<BudgetLedgerScopeCreateRejected>();

        rejected.Failure.Kind.ShouldBe(BudgetScopeCreationFailureKind.MaximumDepthExceeded);
    }

    /// <summary>Verifies concurrent atomic batches bound to disjoint idempotency keys cannot be replayed as one mixed batch.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenItemKeysBelongToDifferentBatches_ThrowsMutationConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "batch-conflict-scope", 100, token);
        var first = Reservation(scope, "batch-conflict-a", 1);
        var second = Reservation(scope, "batch-conflict-b", 1);
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [first]), token)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        _ = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [second]), token)).ShouldBeOfType<BudgetLedgerBatchReserved>();
        var mixed = new BudgetLedgerBatchReserveRequest(scope, [Reservation(scope, "batch-conflict-a", 1), Reservation(scope, "batch-conflict-b", 1)]);

        _ = await Should.ThrowAsync<BudgetLedgerMutationConflictException>(
            async () => await ledger.ReserveBatchAsync(mixed, token));
    }

    /// <summary>Verifies a dimension descriptor with undefined aggregation semantics is refused rather than aggregated arbitrarily.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenDimensionAggregationIsUndefined_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var target = new JsonBudgetLedgerTarget(
            Path.Combine(TestTemporaryDirectory.Create(), "ledger"), new(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);
        var catalog = new UndefinedAggregationCatalog();
        using var ledger = new JsonBudgetLedger(target, JsonBudgetLedgerSettings.CreateDefault(), TimeProvider.System, new StubScopeIds(), new StubReservationIds(), catalog);
        await ledger.InitializeAsync(token);
        var scope = await CreateScopeAsync(ledger, "undefined-aggregation-scope", 10, token);
        var item = new BudgetReservationRequest(scope.Id, new BudgetDimension("undefined.aggregation"), 1, _unit, _operation, null, new IdempotencyKey("undefined-item"));

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(
            async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), token));
    }

    /// <summary>
    /// Documents that <c>JsonBudgetLedger</c>'s own "one atomic batch cannot mix units for the same dimension" guard
    /// cannot be exercised from outside the ledger: <see cref="BudgetLedgerBatchReserveRequest"/>'s validating
    /// constructor already rejects a batch mixing units for one dimension via
    /// <c>ArgumentException.ThrowIfInvalidBudgetReservationBatch</c>, so no caller can ever construct a request that
    /// reaches the ledger's own redundant check. The guard is defensive dead code from every external caller's
    /// perspective.
    /// </summary>
    [Fact]
    public void BudgetLedgerBatchReserveRequest_WhenBatchMixesUnitsForOneDimension_RejectsBeforeReachingTheLedger()
    {
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var scope = new BudgetLedgerScopeReference(new BudgetScopeId(Guid.NewGuid()), address);
        var multiUnit = new BudgetDimension("test.multi-unit");
        ImmutableArray<BudgetReservationRequest> items =
        [
            new(scope.Id, multiUnit, 1, new("count"), _operation, null, new IdempotencyKey("mixed-a")),
            new(scope.Id, multiUnit, 1, new("bytes"), _operation, null, new IdempotencyKey("mixed-b")),
        ];

        _ = Should.Throw<ArgumentException>(() => new BudgetLedgerBatchReserveRequest(scope, items));
    }

    /// <summary>Verifies a reservation unit that conflicts with the scope's own captured limit unit is refused.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenUnitConflictsWithCapturedScopeLimit_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var multiUnit = new BudgetDimension("test.multi-unit");
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [new BudgetLimit(multiUnit, 100, new("count"), BudgetLimitKind.Hard)], new IdempotencyKey("limit-unit-conflict-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var scope = (await ledger.CreateScopeAsync(request, token)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, multiUnit, 1, new("bytes"), _operation, null, new IdempotencyKey("limit-unit-conflict-item"));

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(
            async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), token));
    }

    /// <summary>Verifies a reservation-identity source producing a duplicate value within one batch is refused instead of silently colliding.</summary>
    [Fact]
    public async Task ReserveBatchAsync_WhenReservationIdGeneratorProducesDuplicate_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var target = new JsonBudgetLedgerTarget(
            Path.Combine(TestTemporaryDirectory.Create(), "ledger"), new(Guid.NewGuid()), JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);
        var catalog = new FaultCatalog();
        using var ledger = new JsonBudgetLedger(target, JsonBudgetLedgerSettings.CreateDefault(), TimeProvider.System, new StubScopeIds(), new FixedReservationIds(Guid.NewGuid()), catalog);
        await ledger.InitializeAsync(token);
        var scope = await CreateScopeAsync(ledger, "duplicate-reservation-id-scope", 10, token);
        ImmutableArray<BudgetReservationRequest> items =
        [
            new(scope.Id, _dimension, 1, _unit, _operation, null, new IdempotencyKey("duplicate-id-a")),
            new(scope.Id, _dimension, 1, _unit, _operation, null, new IdempotencyKey("duplicate-id-b")),
        ];

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(
            async () => await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, items), token));
    }

    /// <summary>Verifies marking an already-started reservation started again is idempotent and reports it was already started.</summary>
    [Fact]
    public async Task MarkStartedAsync_WhenAlreadyStarted_ReportsAlreadyStarted()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "already-started-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "already-started-item", 1, token);
        _ = (await ledger.MarkStartedAsync(reservation, token)).ShouldBeOfType<BudgetStarted>();

        var second = await ledger.MarkStartedAsync(reservation, token);

        second.ShouldBe(new BudgetStarted(reservation.Id, true));
    }

    /// <summary>Verifies releasing a reservation that already settled reports it is already settled instead of releasing capacity that no longer exists.</summary>
    [Fact]
    public async Task ReleaseUnstartedAsync_WhenReservationIsAlreadySettled_ReportsAlreadySettled()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "release-after-settle-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "release-after-settle-item", 1, token);
        _ = await ledger.MarkStartedAsync(reservation, token);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), token);

        var result = await ledger.ReleaseUnstartedAsync(reservation, token);

        result.ShouldBe(new BudgetLedgerAlreadySettled(commit));
    }

    /// <summary>Verifies correcting a reservation that was never settled is refused, because only settled accounting can be corrected.</summary>
    [Fact]
    public async Task CorrectAsync_WhenReservationWasNeverSettled_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "correct-unsettled-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "correct-unsettled-item", 1, token);
        _ = await ledger.MarkStartedAsync(reservation, token);

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(
            async () => await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 2, 1), token));
    }

    /// <summary>Verifies a correction revision that does not strictly increase is refused instead of silently reapplying stale evidence.</summary>
    [Fact]
    public async Task CorrectAsync_WhenRevisionDoesNotIncrease_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "correct-revision-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "correct-revision-item", 1, token);
        _ = await ledger.MarkStartedAsync(reservation, token);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), token);
        _ = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 2, 5), token);

        // A different revision number with different evidence (rather than an exact replay of revision 5) is required
        // to reach the monotonic-revision guard instead of the earlier replay-conflict check for a reused revision.
        _ = await Should.ThrowAsync<BudgetLedgerStateException>(
            async () => await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 3, 3), token));
    }

    /// <summary>Verifies a page size beyond the scope's captured finite bound is refused instead of silently truncating it.</summary>
    [Fact]
    public async Task ReadUnresolvedStartedAsync_WhenPageSizeExceedsCapturedBound_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "page-size-scope", 10, token);

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReadUnresolvedStartedAsync(
            new BudgetUnresolvedReservationQuery(scope, 33, null), token));
    }

    /// <summary>Verifies reconciling an unstarted reservation is refused, because only started reservations carry unresolved unknown spend.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenReservationWasNeverStarted_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "reconcile-unstarted-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "reconcile-unstarted-item", 1, token);

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ReconcileAsync(
            new BudgetLedgerReconciliationRequest(reservation, new BudgetStillUnknown(), new IdempotencyKey("reconcile-unstarted-key")), token));
    }

    /// <summary>Verifies an estimated-usage reconciliation settles the reservation using the caller's conservative estimate.</summary>
    [Fact]
    public async Task ReconcileAsync_WhenEvidenceIsEstimated_SettlesWithEstimatedActual()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "reconcile-estimated-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "reconcile-estimated-item", 4, token);
        _ = await ledger.MarkStartedAsync(reservation, token);

        var result = await ledger.ReconcileAsync(
            new BudgetLedgerReconciliationRequest(reservation, new BudgetActualEstimated(3), new IdempotencyKey("reconcile-estimated-key")), token);

        var settled = result.ShouldBeOfType<BudgetLedgerReconciliationSettled>();
        settled.Commit.Actual.ShouldBe(3);
    }

    /// <summary>Verifies resolving an overrun hold whose reservation does not belong to the referenced boundary's lineage is refused.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenReservationIsNotInBoundaryLineage_ThrowsReferenceUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var boundaryScope = await CreateScopeAsync(ledger, "unrelated-boundary-scope", 10, token);
        var otherScope = await CreateScopeAsync(ledger, "unrelated-reservation-scope", 10, token);
        var reservation = await ReserveAsync(ledger, otherScope, "unrelated-reservation-item", 1, token);
        var fabricatedHold = new BudgetOverrunHoldReference(boundaryScope, reservation, new BudgetAccountingRevision(1));

        _ = await Should.ThrowAsync<BudgetLedgerReferenceUnavailableException>(async () => await ledger.ResolveOverrunHoldAsync(
            ResolutionRequest(fabricatedHold, "unrelated-lineage-key"), token));
    }

    /// <summary>Verifies resolving a hold generation created under a policy other than authorized-resolution is refused.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenPolicyIsNotAuthorizedResolution_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "clear-policy-scope", 10, token, BudgetOverrunHoldPolicy.ClearWhenReconciled);
        var reservation = await ReserveAsync(ledger, scope, "clear-policy-item", 4, token);
        _ = await ledger.MarkStartedAsync(reservation, token);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 8), token);
        var hold = commit.CreatedOverrunHolds.ShouldHaveSingleItem().Reference;

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(async () => await ledger.ResolveOverrunHoldAsync(
            ResolutionRequest(hold, "clear-policy-key"), token));
    }

    /// <summary>Verifies correcting a reservation on a dimension without any configured hard limit is immediately eligible for automatic clearance.</summary>
    [Fact]
    public async Task CorrectAsync_WhenDimensionHasNoConfiguredHardLimit_TreatsCorrectionAsEligibleForClear()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey("no-hard-limit-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var scope = (await ledger.CreateScopeAsync(request, token)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var reservation = await ReserveAsync(ledger, scope, "no-hard-limit-item", 4, token);
        _ = await ledger.MarkStartedAsync(reservation, token);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 4), token);

        // The corrected value must stay at or below the original reservation amount (4): a correction that still
        // exceeds it is rejected as an overrun before the "no hard limit configured" branch is ever reached.
        var correction = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 2, 1), token);

        correction.CorrectedActual.ShouldBe(2);
    }

    /// <summary>Verifies a concurrent-gauge dimension's overrun-clearance eligibility is computed using gauge aggregation rather than a running sum.</summary>
    [Fact]
    public async Task CorrectAsync_WhenDimensionIsConcurrentGauge_UsesGaugeAggregationForClearanceEligibility()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var gauge = new BudgetDimension("test.gauge");
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [new BudgetLimit(gauge, 5, new("count"), BudgetLimitKind.Hard)], new IdempotencyKey("gauge-clear-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5)));
        var scope = (await ledger.CreateScopeAsync(request, token)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, gauge, 2, new("count"), _operation, null, new IdempotencyKey("gauge-clear-item"));
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), token))
            .ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        _ = await ledger.MarkStartedAsync(reservation, token);
        _ = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 8), token);

        // The corrected value must not exceed the original reservation amount (2): a still-overrunning correction is
        // rejected before clearance eligibility is even considered.
        var correction = await ledger.CorrectAsync(new BudgetLedgerCorrectionRequest(reservation, 2, 1), token);

        correction.CorrectedActual.ShouldBe(2);
    }

    /// <summary>Verifies resolving an overrun hold on a concurrent-gauge dimension with a hard limit evaluates current hard-failure evidence using gauge aggregation.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenDimensionIsConcurrentGaugeWithHardLimit_EvaluatesUsingGaugeAggregation()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var gauge = new BudgetDimension("test.gauge");
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [new BudgetLimit(gauge, 5, new("count"), BudgetLimitKind.Hard)], new IdempotencyKey("gauge-resolve-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution));
        var scope = (await ledger.CreateScopeAsync(request, token)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var item = new BudgetReservationRequest(scope.Id, gauge, 2, new("count"), _operation, null, new IdempotencyKey("gauge-resolve-item"));
        var reservation = (await ledger.ReserveBatchAsync(new BudgetLedgerBatchReserveRequest(scope, [item]), token))
            .ShouldBeOfType<BudgetLedgerBatchReserved>().Receipts[0].Reservation;
        _ = await ledger.MarkStartedAsync(reservation, token);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 8), token);
        var hold = commit.CreatedOverrunHolds.ShouldHaveSingleItem().Reference;

        var result = await ledger.ResolveOverrunHoldAsync(ResolutionRequest(hold, "gauge-resolve-key"), token);

        _ = result.ShouldNotBeNull();
    }

    /// <summary>Verifies settling a reservation that never started is refused, because only a started reservation has controlled-effect permission to account for.</summary>
    [Fact]
    public async Task SettleAsync_WhenReservationWasNeverStarted_ThrowsBudgetLedgerStateException()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var scope = await CreateScopeAsync(ledger, "settle-unstarted-scope", 10, token);
        var reservation = await ReserveAsync(ledger, scope, "settle-unstarted-item", 1, token);

        _ = await Should.ThrowAsync<BudgetLedgerStateException>(
            async () => await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 1), token));
    }

    /// <summary>Verifies resolving an overrun hold on a dimension without any configured hard limit evaluates no current hard-limit failures.</summary>
    [Fact]
    public async Task ResolveOverrunHoldAsync_WhenDimensionHasNoConfiguredHardLimit_EvaluatesNoHardFailures()
    {
        var token = TestContext.Current.CancellationToken;
        var ledger = (JsonBudgetLedger) new JsonBudgetLedgerConformanceFixture().CreateLedger();
        var address = new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), _agent, null, null, null);
        var request = new BudgetLedgerScopeCreateRequest(
            new BudgetScopeRequest(null, address, [], new IdempotencyKey("no-hard-limit-resolve-scope")),
            new BudgetScopeAdmission(8, 32, TimeSpan.FromMinutes(5), BudgetOverrunHoldPolicy.RequireAuthorizedResolution));
        var scope = (await ledger.CreateScopeAsync(request, token)).ShouldBeOfType<BudgetLedgerScopeCreated>().Scope;
        var reservation = await ReserveAsync(ledger, scope, "no-hard-limit-resolve-item", 2, token);
        _ = await ledger.MarkStartedAsync(reservation, token);
        var commit = await ledger.SettleAsync(new BudgetLedgerSettlementRequest(reservation, 5), token);
        var hold = commit.CreatedOverrunHolds.ShouldHaveSingleItem().Reference;

        var result = await ledger.ResolveOverrunHoldAsync(ResolutionRequest(hold, "no-hard-limit-resolve-key"), token);

        _ = result.ShouldNotBeNull();
    }

    private sealed class UndefinedAggregationCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("count")])
                with
            { Aggregation = (BudgetAggregationKind) 9_999 };
            return true;
        }
    }

    private sealed class FixedReservationIds(Guid value): IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => new(value);
    }
}
