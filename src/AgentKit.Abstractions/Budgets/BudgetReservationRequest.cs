// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to atomically reserve capacity against a
/// <see cref="BudgetDimension"/> within a budget scope.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Submitting a request with the same
/// <see cref="IdempotencyKey"/> against the same authority returns the original
/// reservation receipt rather than reserving twice, including after release or
/// expiry. The first-party in-memory authority retains that binding only for
/// its process lifetime; persistence across process loss requires a durable
/// ledger implementation.
/// </remarks>
public sealed record BudgetReservationRequest
{
    /// <summary>Initializes a new instance of the <see cref="BudgetReservationRequest"/> record.</summary>
    /// <param name="scopeId">The scope to reserve against.</param>
    /// <param name="dimension">The dimension to reserve capacity for.</param>
    /// <param name="amount">The positive amount of capacity to reserve.</param>
    /// <param name="unit">The unit <paramref name="amount"/> is expressed in.</param>
    /// <param name="operationId">The operation this reservation is made on behalf of.</param>
    /// <param name="expiresAt">
    /// The instant after which an uncommitted reservation is treated as
    /// released, when the caller supplies one; otherwise the authority
    /// applies its configured default lifetime.
    /// </param>
    /// <param name="idempotencyKey">The key that makes repeating this exact request safe.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive.</exception>
    public BudgetReservationRequest(
        BudgetScopeId scopeId,
        BudgetDimension dimension,
        decimal amount,
        BudgetUnit unit,
        OperationId operationId,
        DateTimeOffset? expiresAt,
        IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        ScopeId = scopeId;
        Dimension = dimension;
        Amount = amount;
        Unit = unit;
        OperationId = operationId;
        ExpiresAt = expiresAt;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the scope to reserve against.</summary>
    public BudgetScopeId ScopeId { get; init; }

    /// <summary>Gets the dimension to reserve capacity for.</summary>
    public BudgetDimension Dimension { get; init; }

    /// <summary>Gets the amount of capacity to reserve.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a non-positive amount.</exception>
    public decimal Amount
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(Amount));
            field = value;
        }
    }

    /// <summary>Gets the unit <see cref="Amount"/> is expressed in.</summary>
    public BudgetUnit Unit { get; init; }

    /// <summary>Gets the operation this reservation is made on behalf of.</summary>
    public OperationId OperationId { get; init; }

    /// <summary>
    /// Gets the instant after which an uncommitted reservation is treated
    /// as released, when the caller supplies one.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the key that makes repeating this exact request safe.</summary>
    public IdempotencyKey IdempotencyKey { get; init; }
}
