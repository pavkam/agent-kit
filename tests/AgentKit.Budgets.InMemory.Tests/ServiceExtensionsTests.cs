// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies the registration extension validates its receiver.</summary>
    [Fact]
    public void AddInMemoryBudgetLedger_WhenServicesIsNull_ThrowsWithExactParameterName()
    {
        IServiceCollection services = null!;
        Should.Throw<ArgumentNullException>(services.AddInMemoryBudgetLedger).ParamName.ShouldBe("services");
    }

    /// <summary>Verifies repeated selection registers one copy of the same leaf.</summary>
    [Fact]
    public void AddInMemoryBudgetLedger_WhenRepeated_IsIdempotent()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemoryBudgetLedger().AddInMemoryBudgetLedger();
        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(1);
    }

    /// <summary>Verifies application-supplied clocks and typed identity sources remain selected.</summary>
    [Fact]
    public void AddInMemoryBudgetLedger_WhenCollaboratorsPreconfigured_PreservesThem()
    {
        var services = new ServiceCollection();
        var clock = new FixedTimeProvider();
        var scopeIds = new ScopeIdGenerator();
        var reservationIds = new ReservationIdGenerator();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton<IIdentifierGenerator<BudgetScopeId>>(scopeIds);
        _ = services.AddSingleton<IIdentifierGenerator<BudgetReservationId>>(reservationIds);
        _ = services.AddSingleton<IBudgetDimensionCatalog, EmptyDimensionCatalog>();
        _ = services.AddInMemoryBudgetLedger();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
        provider.GetRequiredService<IIdentifierGenerator<BudgetScopeId>>().ShouldBeSameAs(scopeIds);
        provider.GetRequiredService<IIdentifierGenerator<BudgetReservationId>>().ShouldBeSameAs(reservationIds);
        _ = provider.GetRequiredService<IBudgetLedger>().ShouldBeOfType<InMemoryBudgetLedger>();
    }

    /// <summary>Verifies an existing custom ledger remains visible so composition can reject ambiguity.</summary>
    [Fact]
    public void AddInMemoryBudgetLedger_WhenCustomLedgerExists_PreservesBothRegistrations()
    {
        var services = new ServiceCollection();
        var custom = new StubBudgetLedger();
        _ = services.AddSingleton<IBudgetLedger>(custom);
        _ = services.AddInMemoryBudgetLedger();
        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(2);
        services.ShouldContain(descriptor => ReferenceEquals(descriptor.ImplementationInstance, custom));
        services.ShouldContain(descriptor => descriptor.ImplementationType == typeof(InMemoryBudgetLedger));
    }

    private sealed class ScopeIdGenerator: IIdentifierGenerator<BudgetScopeId>
    {
        public BudgetScopeId Create() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    }

    private sealed class ReservationIdGenerator: IIdentifierGenerator<BudgetReservationId>
    {
        public BudgetReservationId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    }

    private sealed class EmptyDimensionCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }
    }

    private sealed class FixedTimeProvider: TimeProvider;
    private sealed class StubBudgetLedger: IBudgetLedger
    {
        public BudgetLedgerDescriptor Descriptor { get; } = new(false, BudgetLedgerConcurrencyDomain.ProcessLocal);

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

}
