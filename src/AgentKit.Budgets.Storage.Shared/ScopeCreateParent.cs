// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Storage;

/// <summary>Provides immutable parent-lineage evidence needed to evaluate a child scope.</summary>
internal sealed record ScopeCreateParent
{
    /// <summary>Creates captured parent evidence in child-to-root order.</summary>
    /// <param name="reference">The exact non-null immediate-parent reference.</param>
    /// <param name="request">The exact non-null immediate-parent creation request.</param>
    /// <param name="depth">The positive immediate-parent depth.</param>
    /// <param name="ancestors">The initialized nonempty lineage beginning with <paramref name="request"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="ancestors"/> is default, empty, contains null, does not begin with <paramref name="request"/>, or its length differs from <paramref name="depth"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not positive.</exception>
    internal ScopeCreateParent(BudgetLedgerScopeReference reference, BudgetLedgerScopeCreateRequest request, int depth, ImmutableArray<BudgetLedgerScopeCreateRequest> ancestors)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentException.ThrowIfDefaultOrEmpty(ancestors);
        ArgumentException.ThrowIfContainsNull(ancestors);
        ArgumentException.ThrowIfNotEqual(ancestors[0], request, nameof(ancestors));
        ArgumentException.ThrowIfNotEqual(ancestors.Length, depth, nameof(ancestors));
        Reference = reference;
        Request = request;
        Depth = depth;
        Ancestors = ancestors;
    }

    /// <summary>Gets the exact immediate-parent reference.</summary><value>The non-null persisted identity and address.</value>
    internal BudgetLedgerScopeReference Reference { get; }
    /// <summary>Gets immediate-parent creation evidence.</summary><value>The first request in <see cref="Ancestors"/>.</value>
    internal BudgetLedgerScopeCreateRequest Request { get; }
    /// <summary>Gets immediate-parent depth.</summary><value>The positive count of <see cref="Ancestors"/>.</value>
    internal int Depth { get; }
    /// <summary>Gets complete parent lineage.</summary><value>A nonempty child-to-root array beginning with <see cref="Request"/>.</value>
    internal ImmutableArray<BudgetLedgerScopeCreateRequest> Ancestors { get; }
}
