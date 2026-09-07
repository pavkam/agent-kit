// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>An immutable, validated copy of <see cref="AgentBudgetOptions"/> captured at composition time.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
internal sealed record AgentBudgetOptionsSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="AgentBudgetOptionsSnapshot"/> record.</summary>
    /// <param name="maximumScopeDepth">The maximum number of hierarchy levels a scope may nest to.</param>
    /// <param name="maximumOpenReservationsPerScope">The maximum number of concurrently open reservations one scope may hold.</param>
    /// <param name="defaultReservationLifetime">The lifetime applied to a reservation with no explicit expiry.</param>
    /// <param name="unknownCostBehavior">How the authority reacts to unknown-cost commitments.</param>
    /// <param name="overrunBehavior">How the authority reacts to a commit that overruns its reservation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumScopeDepth"/> or <paramref name="maximumOpenReservationsPerScope"/> is not positive,
    /// <paramref name="defaultReservationLifetime"/> is not positive, or
    /// <paramref name="unknownCostBehavior"/> or <paramref name="overrunBehavior"/> is undefined.
    /// </exception>
    public AgentBudgetOptionsSnapshot(
        int maximumScopeDepth,
        int maximumOpenReservationsPerScope,
        TimeSpan defaultReservationLifetime,
        BudgetUnknownCostBehavior unknownCostBehavior,
        BudgetOverrunBehavior overrunBehavior)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumScopeDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOpenReservationsPerScope);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(defaultReservationLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfUndefined(unknownCostBehavior);
        ArgumentOutOfRangeException.ThrowIfUndefined(overrunBehavior);

        MaximumScopeDepth = maximumScopeDepth;
        MaximumOpenReservationsPerScope = maximumOpenReservationsPerScope;
        DefaultReservationLifetime = defaultReservationLifetime;
        UnknownCostBehavior = unknownCostBehavior;
        OverrunBehavior = overrunBehavior;
    }

    /// <summary>Gets the maximum number of hierarchy levels a scope may nest to.</summary>
    public int MaximumScopeDepth { get; }

    /// <summary>Gets the maximum number of concurrently open reservations one scope may hold.</summary>
    public int MaximumOpenReservationsPerScope { get; }

    /// <summary>Gets the lifetime applied to a reservation with no explicit expiry.</summary>
    public TimeSpan DefaultReservationLifetime { get; }

    /// <summary>Gets how the authority reacts to unknown-cost commitments.</summary>
    public BudgetUnknownCostBehavior UnknownCostBehavior { get; }

    /// <summary>Gets how the authority reacts to a commit that overruns its reservation.</summary>
    public BudgetOverrunBehavior OverrunBehavior { get; }
}
