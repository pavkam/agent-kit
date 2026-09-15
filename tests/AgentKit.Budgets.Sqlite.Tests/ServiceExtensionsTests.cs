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

        Should.Throw<ArgumentNullException>(() => services.AddSqliteBudgetLedger(null!, (SqliteBudgetLedgerSettings) null!)).ParamName.ShouldBe("services");
    }

    /// <summary>Proves the configure overload attributes a null caller collection before running the delegate.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenServicesIsNullAndConfigureOverload_ThrowsExactParameter()
    {
        IServiceCollection services = null!;
        var invoked = false;

        Should.Throw<ArgumentNullException>(() => services.AddSqliteBudgetLedger(CreateTarget(), _ => invoked = true)).ParamName.ShouldBe("services");
        invoked.ShouldBeFalse();
    }

    /// <summary>Proves the configure overload attributes a null target before running the delegate or registering anything.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenTargetIsNullAndConfigureOverload_ThrowsExactParameter()
    {
        var services = new ServiceCollection();
        var invoked = false;

        Should.Throw<ArgumentNullException>(() => services.AddSqliteBudgetLedger(null!, _ => invoked = true)).ParamName.ShouldBe("target");
        invoked.ShouldBeFalse();
        services.Count.ShouldBe(0);
    }

    /// <summary>Proves calling with only a target binds to the configure overload and captures default settings.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenConfigureIsOmitted_CapturesDefaultSettings()
    {
        var services = new ServiceCollection();

        _ = services.AddSqliteBudgetLedger(CreateTarget());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<SqliteBudgetLedgerSettings>().ShouldBe(SqliteBudgetLedgerSettings.CreateDefault());
        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(1);
    }

    /// <summary>Proves a configure delegate's values reach the captured immutable settings singleton.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenConfigureIsSupplied_CapturesConfiguredSettings()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();

        _ = services.AddSqliteBudgetLedger(target, options =>
        {
            options.LockTimeout = TimeSpan.FromSeconds(9);
            options.MaximumPayloadBytes = 1_024;
            options.MaximumResultBytes = 2_048;
            options.MaximumBatchSize = 7;
            options.MaximumLimitsPerScope = 11;
            options.MaximumLineageDepth = 3;
        });

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<SqliteBudgetLedgerSettings>();
        settings.ShouldBe(new SqliteBudgetLedgerSettings(TimeSpan.FromSeconds(9), 1_024, 2_048, 7, 11, 3));
        settings.MaximumBatchSize.ShouldBe(7);
        provider.GetRequiredService<SqliteBudgetLedgerTarget>().ShouldBeSameAs(target);
    }

    /// <summary>Proves an invalid configured bound is rejected at registration before any descriptor is added.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenConfiguredValueIsInvalid_ThrowsBeforeRegistering()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IBudgetLedger, StubLedger>();
        var countBefore = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => services.AddSqliteBudgetLedger(CreateTarget(), static options => options.MaximumBatchSize = 0));

        exception.ParamName.ShouldBe("maximumBatchSize");
        services.Count.ShouldBe(countBefore);
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(SqliteBudgetLedgerTarget));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(SqliteBudgetLedgerSettings));
    }

    /// <summary>Proves an invalid configured lock timeout is rejected with the settings parameter name.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenConfiguredLockTimeoutIsFractional_ThrowsBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => services.AddSqliteBudgetLedger(CreateTarget(), static options => options.LockTimeout = TimeSpan.FromMilliseconds(1_500)));

        exception.ParamName.ShouldBe("lockTimeout");
        services.Count.ShouldBe(0);
    }

    /// <summary>Proves repeating the configure overload with the same effective settings is idempotent.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenSameConfigureRepeated_IsIdempotent()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        static void Configure(SqliteBudgetLedgerOptions options) => options.MaximumBatchSize = 7;

        _ = services.AddSqliteBudgetLedger(target, Configure);
        var countAfterFirst = services.Count;
        _ = services.AddSqliteBudgetLedger(target, Configure);

        services.Count.ShouldBe(countAfterFirst);
        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(SqliteBudgetLedgerSettings)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<SqliteBudgetLedgerSettings>().MaximumBatchSize.ShouldBe(7);
    }

    /// <summary>Proves repeating the configure overload with different effective settings is rejected.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenDifferentConfigureRepeated_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        _ = services.AddSqliteBudgetLedger(target, static options => options.MaximumBatchSize = 7);

        _ = Should.Throw<InvalidOperationException>(() => services.AddSqliteBudgetLedger(target, static options => options.MaximumBatchSize = 8));
    }

    /// <summary>Proves the configure overload and the explicit settings overload agree on equality for conflict detection.</summary>
    [Fact]
    public void AddSqliteBudgetLedger_WhenConfigureMatchesExplicitSettings_IsIdempotentAcrossOverloads()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        _ = services.AddSqliteBudgetLedger(target, SqliteBudgetLedgerSettings.CreateDefault());
        var countAfterFirst = services.Count;

        _ = services.AddSqliteBudgetLedger(target);

        services.Count.ShouldBe(countAfterFirst);
    }

    private static SqliteBudgetLedgerTarget CreateTarget() => new(
        Path.Combine(Path.GetTempPath(), "budget.db"),
        new(Guid.NewGuid()),
        SqliteDatabaseOpenMode.OpenExisting,
        SqliteSchemaMode.ValidateExact);

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
