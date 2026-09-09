// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies explicit additive SQLite ledger composition.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Proves the extension attributes a null caller collection before inspecting configuration.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenServicesIsNull_ThrowsExactParameter()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddSqliteBudgetLedger(null!, null!)).ParamName.ShouldBe("services");
    }

    /// <summary>Proves exact repeats are idempotent while a competing ledger remains visible.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenRepeatedAndCompeting_PreservesVisibleSelections()
    {
        var services = new ServiceCollection();
        var scopes = new ScopeIds();
        var reservations = new ReservationIds();
        _ = services.AddSingleton<IIdentifierGenerator<BudgetScopeId>>(scopes);
        _ = services.AddSingleton<IIdentifierGenerator<BudgetReservationId>>(reservations);
        var target = new SqliteBudgetLedgerTarget(Path.Combine(Path.GetTempPath(), "budget.db"), new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact);
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        _ = services.AddSqliteBudgetLedger(target, settings);
        _ = services.AddSqliteBudgetLedger(target, settings);
        _ = services.AddSingleton<IBudgetLedger, StubLedger>();

        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(2);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdentifierGenerator<BudgetScopeId>>().ShouldBeSameAs(scopes);
        provider.GetRequiredService<IIdentifierGenerator<BudgetReservationId>>().ShouldBeSameAs(reservations);
    }

    /// <summary>Proves conflicting fixed-target evidence is rejected during composition without storage access.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenTargetChanges_ThrowsBeforeStorageAccess()
    {
        var services = new ServiceCollection();
        var settings = SqliteBudgetLedgerSettings.CreateDefault();
        _ = services.AddSqliteBudgetLedger(new("/does/not/exist/a.db", new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), settings);

        _ = Should.Throw<InvalidOperationException>(() => services.AddSqliteBudgetLedger(new("/also/missing/b.db", new(Guid.NewGuid()), SqliteDatabaseOpenMode.OpenExisting, SqliteSchemaMode.ValidateExact), settings));
    }

    private sealed class StubLedger: IBudgetLedger
    {
        public BudgetLedgerDescriptor Descriptor => new(false, BudgetLedgerConcurrencyDomain.ProcessLocal);
        public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ScopeIds: IIdentifierGenerator<BudgetScopeId> { public BudgetScopeId Create() => new(Guid.NewGuid()); }
    private sealed class ReservationIds: IIdentifierGenerator<BudgetReservationId> { public BudgetReservationId Create() => new(Guid.NewGuid()); }
}
