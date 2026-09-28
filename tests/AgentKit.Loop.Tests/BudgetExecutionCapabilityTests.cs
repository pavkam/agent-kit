// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

public sealed class BudgetExecutionCapabilityTests
{
    [Fact]
    public void BudgetExecutionCapability_WhenAfterRunCorrelationOmitsRunId_RejectsBinding()
    {
        var scope = new FakeBudgetScope(
            new BudgetScopeId(Guid.NewGuid()),
            new BudgetScopeAddress(new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, new OperationId(Guid.NewGuid())));
        var identity = TestFactory.Identity();
        var afterRun = new AfterRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()));

        var exception = Should.Throw<ArgumentException>(() => new BudgetExecutionCapability(
            new BudgetProfileKey("standard"),
            new BudgetProfileVersion(1),
            identity,
            afterRun,
            scope));

        exception.ParamName.ShouldBe("scope");
    }

    private sealed class FakeBudgetScope(BudgetScopeId id, BudgetScopeAddress address): IBudgetScope
    {
        public BudgetScopeId Id { get; } = id;
        public BudgetScopeAddress Address { get; } = address;

        public ValueTask<BudgetReservationResult> ReserveAsync(BudgetReservationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
