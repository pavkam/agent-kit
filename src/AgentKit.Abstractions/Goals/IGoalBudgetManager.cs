// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reserves and settles hierarchical child budgets for delegated goals.</summary>
/// <remarks>
/// A child's ceiling must fit inside what its parent holds, and host options are hard limits. Reservations survive
/// disposal of the parent's call until settled: a waiting parent releases worker occupancy, not spent or reserved child
/// budget. Implementations are thread-safe and may be singletons.
/// </remarks>
public interface IGoalBudgetManager
{
    /// <summary>Reserves a child's requested budget.</summary>
    /// <param name="request">The reserve request.</param>
    /// <param name="cancellationToken">Cancels the reservation.</param>
    /// <returns>The reservation, or a rejection when the ceiling does not fit the parent or the host limits. Replaying the same delegation returns the same reservation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalBudgetReserveResult> ReserveAsync(GoalBudgetReserveRequest request, CancellationToken cancellationToken = default);

    /// <summary>Settles a reservation with the usage its holder reported.</summary>
    /// <param name="request">The settle request.</param>
    /// <param name="cancellationToken">Cancels the settlement.</param>
    /// <returns>Whether the known usage stayed inside the reserved ceiling.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalBudgetSettlement> SettleAsync(GoalBudgetSettleRequest request, CancellationToken cancellationToken = default);
}
