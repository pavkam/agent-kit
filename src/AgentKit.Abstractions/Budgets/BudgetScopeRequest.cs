// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to create a budget scope, optionally as
/// the child of an existing scope.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller profile-selection
/// shape described by the budgets architecture, which additionally resolves
/// limits and ordered policy keys from a keyed
/// <c>IBudgetProfileCatalog</c>/<c>IBudgetPolicyCatalog</c> pair. Until that
/// catalog infrastructure exists — it depends on the not-yet-implemented
/// keyed-selection (<c>ComponentKey&lt;T&gt;</c>) machinery — a caller
/// supplies the scope's limits directly.
/// </para>
/// </remarks>
public sealed record BudgetScopeRequest
{
    /// <summary>Initializes a new instance of the <see cref="BudgetScopeRequest"/> record.</summary>
    /// <param name="parentScopeId">
    /// The scope this new scope is a child of, when applicable;
    /// <see langword="null"/> creates a root scope.
    /// </param>
    /// <param name="address">The hierarchical address of the new scope.</param>
    /// <param name="limits">The limits configured directly on the new scope.</param>
    /// <param name="idempotencyKey">The key that makes repeating this exact request safe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="limits"/> is a default, uninitialized array.</exception>
    public BudgetScopeRequest(
        BudgetScopeId? parentScopeId,
        BudgetScopeAddress address,
        ImmutableArray<BudgetLimit> limits,
        IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfDefault(limits);

        ParentScopeId = parentScopeId;
        Address = address;
        Limits = limits;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    /// Gets the scope this new scope is a child of, when applicable;
    /// <see langword="null"/> creates a root scope.
    /// </summary>
    public BudgetScopeId? ParentScopeId { get; init; }

    /// <summary>Gets the hierarchical address of the new scope.</summary>
    public BudgetScopeAddress Address
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the limits configured directly on the new scope.</summary>
    public ImmutableArray<BudgetLimit> Limits
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value);
            field = value;
        }
    }

    /// <summary>Gets the key that makes repeating this exact request safe.</summary>
    public IdempotencyKey IdempotencyKey { get; init; }

    /// <inheritdoc/>
    public bool Equals(BudgetScopeRequest? other) =>
        other is not null
        && Nullable.Equals(ParentScopeId, other.ParentScopeId)
        && Address.Equals(other.Address)
        && Limits.SequenceEqual(other.Limits)
        && IdempotencyKey.Equals(other.IdempotencyKey);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ParentScopeId);
        hash.Add(Address);
        foreach (var limit in Limits)
        {
            hash.Add(limit);
        }

        hash.Add(IdempotencyKey);
        return hash.ToHashCode();
    }
}
