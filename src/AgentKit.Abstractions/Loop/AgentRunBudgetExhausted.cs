// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The run stopped because a reservation against one of its budget limits was refused.</summary>
/// <remarks>
/// <para>
/// The loop reserves a run's declared budget dimensions before each turn, model request, and tool call, and accounts
/// reported usage after each model response. A refused reservation settles the run with this outcome: work already
/// committed stays committed, nothing further is attempted, and the dimension that ran out is named so a host can
/// tell a turn cap from a cost cap.
/// </para>
/// <para>
/// <see cref="Failure"/> is present when the authority rejected the reservation outright; it is
/// <see langword="null"/> when the authority recorded an overrun and held further reservations, in which case
/// <see cref="Dimension"/> and <see cref="SafeMessage"/> still name the cause.
/// </para>
/// </remarks>
public sealed record AgentRunBudgetExhausted: AgentRunOutcome
{
    /// <summary>Initializes the outcome from a rejected reservation.</summary>
    /// <param name="failure">The typed limit failure the authority returned.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public AgentRunBudgetExhausted(BudgetLimitFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
        Dimension = failure.Dimension;
        SafeMessage = failure.SafeMessage;
    }

    /// <summary>Initializes the outcome from a held overrun.</summary>
    /// <param name="dimension">The dimension whose overrun was held.</param>
    /// <param name="safeMessage">A content-safe description.</param>
    /// <exception cref="ArgumentException"><paramref name="dimension"/> or <paramref name="safeMessage"/> is blank.</exception>
    public AgentRunBudgetExhausted(BudgetDimension dimension, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension.Value, nameof(dimension));
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Dimension = dimension;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the typed limit failure, when the reservation was rejected outright.</summary>
    public BudgetLimitFailure? Failure { get; }

    /// <summary>Gets the dimension that ran out.</summary>
    public BudgetDimension Dimension { get; }

    /// <summary>Gets the content-safe description of the exhaustion.</summary>
    public string SafeMessage { get; }
}
