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
/// Callers may supply limits directly as an inline profile, select a named
/// <see cref="Profile"/> resolved by <see cref="IBudgetProfileCatalog"/>, or
/// combine a named profile with additional inline limits that attenuate the
/// resolved profile at scope creation.
/// </para>
/// </remarks>
public sealed record BudgetScopeRequest
{
    /// <summary>Initializes a scope request with inline limits (an inline profile).</summary>
    /// <param name="parentScopeId">
    /// The scope this new scope is a child of, when applicable;
    /// <see langword="null"/> creates a root scope.
    /// </param>
    /// <param name="address">The hierarchical address of the new scope.</param>
    /// <param name="limits">The limits configured directly on the new scope.</param>
    /// <param name="idempotencyKey">The key that makes repeating this exact request safe.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is null, <paramref name="idempotencyKey"/> is default, or a limit has a default dimension or unit.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="limits"/> is default, contains null, contains blank dimension or unit text, or repeats a dimension;
    /// or <paramref name="idempotencyKey"/> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="parentScopeId"/> is present and default, or a limit is negative or has an undefined kind.
    /// </exception>
    public BudgetScopeRequest(
        BudgetScopeId? parentScopeId,
        BudgetScopeAddress address,
        ImmutableArray<BudgetLimit> limits,
        IdempotencyKey idempotencyKey)
        : this(parentScopeId, address, profile: null, limits, idempotencyKey, validateProfile: false)
    {
    }

    /// <summary>Initializes a scope request that resolves limits from a named profile.</summary>
    /// <param name="parentScopeId">The optional parent scope identity.</param>
    /// <param name="address">The hierarchical address of the new scope.</param>
    /// <param name="profile">The nondefault named profile to resolve.</param>
    /// <param name="limits">
    /// Optional inline limits that attenuate the resolved profile; empty when the profile limits alone apply.
    /// </param>
    /// <param name="idempotencyKey">The key that makes repeating this exact request safe.</param>
    /// <exception cref="ArgumentException"><paramref name="profile"/> is default or blank.</exception>
    public BudgetScopeRequest(
        BudgetScopeId? parentScopeId,
        BudgetScopeAddress address,
        BudgetProfileKey profile,
        ImmutableArray<BudgetLimit> limits,
        IdempotencyKey idempotencyKey)
        : this(parentScopeId, address, profile, limits, idempotencyKey, validateProfile: true)
    {
    }

    private BudgetScopeRequest(
        BudgetScopeId? parentScopeId,
        BudgetScopeAddress address,
        BudgetProfileKey? profile,
        ImmutableArray<BudgetLimit> limits,
        IdempotencyKey idempotencyKey,
        bool validateProfile)
    {
        if (parentScopeId is { } parent)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(parent, default, nameof(parentScopeId));
        }

        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        if (validateProfile)
        {
            var profileKey = profile!.Value;
            ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profile));
        }

        ParentScopeId = parentScopeId;
        Address = address;
        Profile = profile is { } named && !string.IsNullOrWhiteSpace(named.Value) ? named : null;
        Limits = limits;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    /// Gets the scope this new scope is a child of, when applicable;
    /// <see langword="null"/> creates a root scope.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a present default identity.</exception>
    public BudgetScopeId? ParentScopeId
    {
        get;
        init
        {
            if (value is { } parent)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(parent, default, nameof(ParentScopeId));
            }

            field = value;
        }
    }

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

    /// <summary>Gets the named profile to resolve, when one was selected.</summary>
    /// <value>A profile key, or <see langword="null"/> for an inline-limits-only request.</value>
    public BudgetProfileKey? Profile { get; init; }

    /// <summary>Gets the limits configured directly on the new scope.</summary>
    /// <exception cref="ArgumentException">An initializer assigns invalid limits or repeats a dimension.</exception>
    /// <exception cref="ArgumentNullException">An initializer assigns a collection containing a default dimension or unit.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a negative limit or undefined limit kind.</exception>
    public ImmutableArray<BudgetLimit> Limits
    {
        get;
        init
        {
            ArgumentException.ThrowIfInvalidBudgetScopeLimits(value, nameof(Limits));
            field = value;
        }
    }

    /// <summary>Gets the key that makes repeating this exact request safe.</summary>
    /// <exception cref="ArgumentException">An initializer assigns a default or blank key.</exception>
    public IdempotencyKey IdempotencyKey
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(IdempotencyKey));
            field = value;
        }
    }

    /// <inheritdoc/>
    public bool Equals(BudgetScopeRequest? other) =>
        other is not null
        && Nullable.Equals(ParentScopeId, other.ParentScopeId)
        && Address.Equals(other.Address)
        && Nullable.Equals(Profile, other.Profile)
        && Limits.SequenceEqual(other.Limits)
        && IdempotencyKey.Equals(other.IdempotencyKey);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ParentScopeId);
        hash.Add(Address);
        hash.Add(Profile);
        foreach (var limit in Limits)
        {
            hash.Add(limit);
        }

        hash.Add(IdempotencyKey);
        return hash.ToHashCode();
    }
}
