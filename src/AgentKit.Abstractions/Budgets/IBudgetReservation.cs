// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An owned handle to one atomically reserved amount of capacity against a
/// single <see cref="BudgetDimension"/>.
/// </summary>
/// <remarks>
/// The reserving component owns this handle for the lifetime of its
/// attempted effect. Immediately before the effect it calls
/// <see cref="MarkStartedAsync"/>. Disposal releases a reservation only while
/// it is unstarted; started capacity remains unresolved until committed, even
/// when the handle is disposed or its lease expires. A successful attempt is
/// settled through <see cref="CommitAsync"/>, and later authoritative evidence
/// replaces that accounting through <see cref="CorrectAsync"/> with monotonic,
/// idempotently replayable revisions. This handle is never captured by a
/// singleton consumer.
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

    /// <summary>Marks the reservation as started immediately before budget-controlled work begins.</summary>
    /// <remarks>
    /// A successful budget transition carries no security authority; the
    /// effecting component still enforces its applicable authorization.
    /// </remarks>
    /// <param name="cancellationToken">A token used to cancel the transition before it occurs.</param>
    /// <returns>A task producing the idempotent start outcome.</returns>
    public ValueTask<BudgetStartResult> MarkStartedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the actual amount consumed, releasing any unused remainder
    /// or recording an overrun above the original reservation.
    /// </summary>
    /// <param name="actual">The non-negative actual amount consumed.</param>
    /// <param name="cancellationToken">A token used to cancel the commit.</param>
    /// <returns>A task producing the settlement outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="actual"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// This reservation was not started, was released before starting, or was
    /// already committed. Disposal after a successful start retains unresolved
    /// accounting and does not prevent later settlement.
    /// </exception>
    public ValueTask<BudgetCommitResult> CommitAsync(decimal actual, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces previously committed actual usage with newer authoritative
    /// accounting for this reservation.
    /// </summary>
    /// <param name="correctedActual">The non-negative corrected actual amount.</param>
    /// <param name="revision">
    /// A positive revision greater than every different correction previously
    /// applied. Replaying the latest revision with identical corrected usage
    /// returns its original receipt without changing accounting.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the correction before it occurs.</param>
    /// <returns>A task producing the applied correction and resulting accounting revision.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="correctedActual"/> is negative or <paramref name="revision"/> is not positive.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The reservation has not been committed; <paramref name="revision"/> is
    /// older than the applied revision; or it reuses the latest revision with
    /// different corrected usage.
    /// </exception>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(
        decimal correctedActual,
        long revision,
        CancellationToken cancellationToken = default);
}
