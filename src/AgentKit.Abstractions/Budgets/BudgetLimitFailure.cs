// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies why one <see cref="BudgetReservationRequest"/> was rejected:
/// the dimension, limit kind, configured value, and the value that was
/// already reserved or committed before this request was evaluated.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record BudgetLimitFailure
{
    /// <summary>Initializes a new instance of the <see cref="BudgetLimitFailure"/> record.</summary>
    /// <param name="scopeId">The scope the rejected request targeted.</param>
    /// <param name="dimension">The dimension whose limit was exceeded.</param>
    /// <param name="kind">The kind of the exceeded limit.</param>
    /// <param name="configuredValue">The configured ceiling value.</param>
    /// <param name="observedValue">
    /// The reserved-plus-committed value already outstanding before this
    /// request, excluding the amount this request asked to reserve.
    /// </param>
    /// <param name="requestedAmount">The amount this request asked to reserve.</param>
    /// <param name="unit">The unit <paramref name="configuredValue"/>, <paramref name="observedValue"/>, and <paramref name="requestedAmount"/> are expressed in.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is undefined, or <paramref name="configuredValue"/>,
    /// <paramref name="observedValue"/>, or <paramref name="requestedAmount"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public BudgetLimitFailure(
        BudgetScopeId scopeId,
        BudgetDimension dimension,
        BudgetLimitKind kind,
        decimal configuredValue,
        decimal observedValue,
        decimal requestedAmount,
        BudgetUnit unit,
        string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfNegative(configuredValue);
        ArgumentOutOfRangeException.ThrowIfNegative(observedValue);
        ArgumentOutOfRangeException.ThrowIfNegative(requestedAmount);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        ScopeId = scopeId;
        Dimension = dimension;
        Kind = kind;
        ConfiguredValue = configuredValue;
        ObservedValue = observedValue;
        RequestedAmount = requestedAmount;
        Unit = unit;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the scope the rejected request targeted.</summary>
    public BudgetScopeId ScopeId { get; init; }

    /// <summary>Gets the dimension whose limit was exceeded.</summary>
    public BudgetDimension Dimension { get; init; }

    /// <summary>Gets the kind of the exceeded limit.</summary>
    public BudgetLimitKind Kind { get; init; }

    /// <summary>Gets the configured ceiling value.</summary>
    public decimal ConfiguredValue { get; init; }

    /// <summary>
    /// Gets the reserved-plus-committed value already outstanding before
    /// this request, excluding the amount this request asked to reserve.
    /// </summary>
    public decimal ObservedValue { get; init; }

    /// <summary>Gets the amount this request asked to reserve.</summary>
    public decimal RequestedAmount { get; init; }

    /// <summary>
    /// Gets the unit <see cref="ConfiguredValue"/>, <see cref="ObservedValue"/>,
    /// and <see cref="RequestedAmount"/> are expressed in.
    /// </summary>
    public BudgetUnit Unit { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
