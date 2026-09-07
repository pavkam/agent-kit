// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

/// <summary>Represents a hostile budget-scope implementation that violates the non-null address contract.</summary>
internal sealed class NullAddressBudgetScope: IBudgetScope
{
    /// <inheritdoc/>
    public BudgetScopeId Id { get; } = new(Guid.Parse("80000000-0000-0000-0000-000000000008"));

    /// <inheritdoc/>
    public BudgetScopeAddress Address => null!;

    /// <inheritdoc/>
    public ValueTask<BudgetReservationResult> ReserveAsync(
        BudgetReservationRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
        ImmutableArray<BudgetReservationRequest> requests,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
