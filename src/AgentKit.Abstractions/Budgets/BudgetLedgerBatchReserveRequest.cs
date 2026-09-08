// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one all-or-none reservation batch using only original caller evidence.</summary>
public sealed record BudgetLedgerBatchReserveRequest
{
    /// <summary>Initializes an atomic reserve transition.</summary>
    /// <param name="scope">The exact non-null receiving scope.</param>
    /// <param name="originalRequests">The initialized, nonempty ordered original requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="originalRequests"/> is not an atomic batch for
    /// <paramref name="scope"/>, carries copied-invalid request evidence,
    /// or names an operation that conflicts with a scope-bound operation.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="originalRequests"/> carries a default request identity
    /// or a nonpositive reservation amount.
    /// </exception>
    public BudgetLedgerBatchReserveRequest(BudgetLedgerScopeReference scope, ImmutableArray<BudgetReservationRequest> originalRequests)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfInvalidBudgetReservationBatch(originalRequests, scope.Id, nameof(originalRequests));
        foreach (var originalRequest in originalRequests)
        {
            ArgumentException.ThrowIfInvalidBudgetLedgerReservationRequest(originalRequest, nameof(originalRequests));
            if (scope.Address.OperationId is { } operationId)
            {
                ArgumentException.ThrowIfNotEqual(originalRequest.OperationId, operationId, nameof(originalRequests));
            }
        }

        Scope = scope;
        OriginalRequests = originalRequests;
    }
    /// <summary>Gets the exact receiving scope.</summary><value>A locator whose address is verified by the adapter.</value>
    public BudgetLedgerScopeReference Scope { get; }
    /// <summary>Gets original requests in fingerprint and receipt order.</summary><value>A nonempty immutable sequence with unique item keys.</value>
    public ImmutableArray<BudgetReservationRequest> OriginalRequests { get; }
    /// <inheritdoc/>
    public bool Equals(BudgetLedgerBatchReserveRequest? other) => other is not null && Scope == other.Scope && OriginalRequests.SequenceEqual(other.OriginalRequests);
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Scope);
        foreach (var request in OriginalRequests)
        {
            hash.Add(request);
        }
        return hash.ToHashCode();
    }
}
