// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

internal static class TestFactory
{
    public static readonly BudgetUnit Count = new("count");

    public static BudgetScopeAddress Address(string tenant = "tenant-1", string principal = "user-1") =>
        new(new TenantId(tenant), new PrincipalId(principal), new AgentId(Guid.NewGuid()), null, null, null);

    public static BudgetScopeRequest ScopeRequest(
        BudgetScopeId? parentScopeId = null,
        ImmutableArray<BudgetLimit>? limits = null,
        string? idempotencyKey = null) =>
        new(
            parentScopeId,
            Address(),
            limits ?? [],
            new IdempotencyKey(idempotencyKey ?? Guid.NewGuid().ToString()));

    public static BudgetLimit HardLimit(BudgetDimension dimension, decimal value, BudgetUnit? unit = null) =>
        new(dimension, value, unit ?? Count, BudgetLimitKind.Hard);

    public static BudgetLimit SoftLimit(BudgetDimension dimension, decimal value, BudgetUnit? unit = null) =>
        new(dimension, value, unit ?? Count, BudgetLimitKind.Soft);

    public static BudgetReservationRequest ReservationRequest(
        BudgetScopeId scopeId,
        BudgetDimension dimension,
        decimal amount = 1m,
        BudgetUnit? unit = null,
        DateTimeOffset? expiresAt = null,
        string? idempotencyKey = null,
        OperationId? operationId = null) =>
        new(
            scopeId,
            dimension,
            amount,
            unit ?? Count,
            operationId ?? new OperationId(Guid.NewGuid()),
            expiresAt,
            new IdempotencyKey(idempotencyKey ?? Guid.NewGuid().ToString()));

    public static InMemoryBudgetAuthority Authority(
        AgentBudgetOptionsSnapshot? options = null,
        TimeProvider? timeProvider = null,
        IBudgetDimensionCatalog? dimensions = null,
        IIdentifierGenerator<BudgetReservationId>? reservationIds = null) =>
        new(
            dimensions ?? DefaultCatalog(),
            new GuidIdentifierGenerator<BudgetScopeId>(static v => new BudgetScopeId(v)),
            reservationIds ?? new GuidIdentifierGenerator<BudgetReservationId>(static v => new BudgetReservationId(v)),
            timeProvider ?? TimeProvider.System,
            options ?? DefaultOptions());

    public static AgentBudgetOptionsSnapshot DefaultOptions(
        int maximumScopeDepth = 16,
        int maximumOpenReservationsPerScope = 256,
        TimeSpan? defaultReservationLifetime = null,
        BudgetOverrunBehavior overrunBehavior = BudgetOverrunBehavior.RecordAndBlockFurtherReservations) =>
        new(
            maximumScopeDepth,
            maximumOpenReservationsPerScope,
            defaultReservationLifetime ?? TimeSpan.FromMinutes(5),
            BudgetUnknownCostBehavior.AllowOnlyWithoutCostLimit,
            overrunBehavior);

    public static IBudgetDimensionCatalog DefaultCatalog(params BudgetDimensionDescriptor[] extra)
    {
        var descriptors = new List<BudgetDimensionDescriptor>
        {
            new(new BudgetDimension("test.dimension"), BudgetAggregationKind.Sum, [Count]),
        };
        descriptors.AddRange(extra);
        return new InMemoryBudgetDimensionCatalog(descriptors);
    }

    public static readonly BudgetDimension TestDimension = new("test.dimension");

    public static async Task<InMemoryBudgetScope> CreateRootScopeAsync(
        InMemoryBudgetAuthority authority, ImmutableArray<BudgetLimit>? limits = null)
    {
        var result = await authority.CreateChildScopeAsync(ScopeRequest(limits: limits));
        return (InMemoryBudgetScope) ((BudgetScopeCreated) result).Scope;
    }
}
