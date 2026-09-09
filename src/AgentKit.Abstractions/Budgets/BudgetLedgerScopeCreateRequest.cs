// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Combines immutable caller evidence with the policy captured when a scope is admitted.</summary>
public sealed record BudgetLedgerScopeCreateRequest
{
    /// <summary>Initializes a scope-create transition.</summary>
    /// <param name="originalRequest">The non-null caller request used for exact replay comparison.</param>
    /// <param name="admission">The non-null captured admission facts persisted with the scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalRequest"/> or <paramref name="admission"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="originalRequest"/> contains invalid limits or blank replay evidence.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="originalRequest"/> contains a present default parent identity, negative limit, or undefined limit kind.</exception>
    public BudgetLedgerScopeCreateRequest(BudgetScopeRequest originalRequest, BudgetScopeAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(originalRequest);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentException.ThrowIfInvalidBudgetScopeLimits(originalRequest.Limits, nameof(originalRequest));
        ArgumentException.ThrowIfNullOrWhiteSpace(originalRequest.IdempotencyKey.Value, nameof(originalRequest));
        if (originalRequest.ParentScopeId is { } parentScopeId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(parentScopeId, default, nameof(originalRequest));
        }

        OriginalRequest = originalRequest;
        Admission = admission;
    }
    /// <summary>Gets immutable caller-owned scope creation evidence.</summary><value>The exact request replayed by its idempotency key.</value>
    public BudgetScopeRequest OriginalRequest { get; }
    /// <summary>Gets the immutable policy selected at admission.</summary><value>Facts the ledger persists instead of rereading mutable runtime options.</value>
    public BudgetScopeAdmission Admission { get; }
}
