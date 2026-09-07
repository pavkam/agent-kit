// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

/// <summary>Supplies immutable scope identity and address evidence without implementing budget behavior.</summary>
internal sealed class TestBudgetScope: IBudgetScope
{
    /// <summary>Initializes exact scope evidence for capability contract tests.</summary>
    /// <param name="id">The scope identity exposed by the fake.</param>
    /// <param name="address">The non-null address exposed by the fake.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    internal TestBudgetScope(BudgetScopeId id, BudgetScopeAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Id = id;
        Address = address;
    }

    /// <inheritdoc/>
    public BudgetScopeId Id { get; }

    /// <inheritdoc/>
    public BudgetScopeAddress Address { get; }

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
