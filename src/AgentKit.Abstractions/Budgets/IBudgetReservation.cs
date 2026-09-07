// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An owned handle to one atomically reserved amount of capacity against a
/// single <see cref="BudgetDimension"/>.
/// </summary>
/// <remarks>
/// The reserving component owns this handle for the lifetime of its
/// attempted effect. It must call <see cref="CommitAsync"/> with the actual
/// amount consumed before returning on success, or dispose the reservation
/// without committing when the attempt never happened or was abandoned.
/// Disposing an uncommitted reservation releases it back to the scope
/// exactly once; disposing an already-committed or already-disposed
/// reservation is idempotent and has no further effect. This handle is
/// never captured by a singleton consumer.
/// </remarks>
public interface IBudgetReservation: IAsyncDisposable
{
    /// <summary>Gets the identity of this reservation.</summary>
    public BudgetReservationId Id { get; }

    /// <summary>Gets the scope this reservation was made against.</summary>
    public BudgetScopeId ScopeId { get; }

    /// <summary>Gets the dimension this reservation was made for.</summary>
    public BudgetDimension Dimension { get; }

    /// <summary>Gets the originally reserved amount.</summary>
    public decimal Reserved { get; }

    /// <summary>
    /// Commits the actual amount consumed, releasing any unused remainder
    /// or recording an overrun above the original reservation.
    /// </summary>
    /// <param name="actual">The non-negative actual amount consumed.</param>
    /// <param name="cancellationToken">A token used to cancel the commit.</param>
    /// <returns>A task producing the settlement outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="actual"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">This reservation was already committed or disposed.</exception>
    public ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default);
}
