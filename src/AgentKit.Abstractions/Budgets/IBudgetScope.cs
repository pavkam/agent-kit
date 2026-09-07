// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One hierarchical budget scope: an owned handle for atomically reserving
/// capacity and observing usage at one address in the shared ledger.
/// </summary>
/// <remarks>
/// A scope enforces its own configured limits and, when it has a parent,
/// atomically enforces the parent's limits as part of the same reservation
/// so effective capacity is always the tightest applicable constraint. A
/// scope is owned by the component that created it through
/// <see cref="IBudgetAuthority"/> and is never captured by a singleton
/// consumer.
/// </remarks>
public interface IBudgetScope
{
    /// <summary>Gets the identity of this scope.</summary>
    public BudgetScopeId Id { get; }

    /// <summary>Gets the hierarchical address this scope occupies.</summary>
    public BudgetScopeAddress Address { get; }

    /// <summary>Atomically reserves capacity against this scope and every enforced ancestor.</summary>
    /// <param name="request">The reservation request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal reservation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<BudgetReservationResult> ReserveAsync(
        BudgetReservationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reserves every requested dimension against this scope and
    /// every enforced ancestor, or reserves none of them.
    /// </summary>
    /// <param name="requests">
    /// The non-empty ordered batch of non-null requests whose members share
    /// this scope, one logical operation, and one compatible unit per dimension.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the operation before admission.</param>
    /// <returns>A task producing the terminal batch reservation outcome.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="requests"/> is default or empty, contains a null member
    /// or duplicate item key, names another scope or operation, or expresses
    /// one dimension in incompatible units.
    /// </exception>
    public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(
        ImmutableArray<BudgetReservationRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>Captures a point-in-time observation of this scope's usage.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the current snapshot.</returns>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
