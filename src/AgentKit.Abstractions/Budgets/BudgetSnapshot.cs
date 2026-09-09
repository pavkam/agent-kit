// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A point-in-time observation of every dimension's usage within one budget
/// scope.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record BudgetSnapshot
{
    /// <summary>Initializes a new instance of the <see cref="BudgetSnapshot"/> record.</summary>
    /// <param name="scopeId">The scope this snapshot describes.</param>
    /// <param name="observedAt">The instant this snapshot was captured.</param>
    /// <param name="usages">The per-dimension usage observed for this scope.</param>
    /// <exception cref="ArgumentException"><paramref name="usages"/> is a default, uninitialized array.</exception>
    public BudgetSnapshot(BudgetScopeId scopeId, DateTimeOffset observedAt, ImmutableArray<BudgetDimensionUsage> usages)
        : this(scopeId, observedAt, usages, [])
    {
    }

    /// <summary>Initializes a point-in-time snapshot including active overrun holds owned by the scope.</summary>
    /// <param name="scopeId">The scope this snapshot describes.</param><param name="observedAt">The observation instant.</param><param name="usages">The local dimension usage.</param><param name="activeOverrunHolds">The active holds owned by this exact boundary.</param>
    /// <exception cref="ArgumentException">An array is default or contains null hold evidence.</exception>
    public BudgetSnapshot(BudgetScopeId scopeId, DateTimeOffset observedAt, ImmutableArray<BudgetDimensionUsage> usages, ImmutableArray<BudgetOverrunHold> activeOverrunHolds)
    {
        ArgumentException.ThrowIfDefault(usages);
        ArgumentException.ThrowIfDefault(activeOverrunHolds);
        ArgumentException.ThrowIfContainsNull(activeOverrunHolds);

        ScopeId = scopeId;
        ObservedAt = observedAt;
        Usages = usages;
        ActiveOverrunHolds = activeOverrunHolds;
    }

    /// <summary>Gets the scope this snapshot describes.</summary>
    public BudgetScopeId ScopeId { get; init; }

    /// <summary>Gets the instant this snapshot was captured.</summary>
    public DateTimeOffset ObservedAt { get; init; }

    /// <summary>Gets the per-dimension usage observed for this scope.</summary>
    public ImmutableArray<BudgetDimensionUsage> Usages { get; init; }

    /// <summary>Gets active overrun holds owned by this exact scope boundary.</summary>
    /// <value>An initialized immutable array; inherited holds remain visible on their owning ancestor snapshots.</value>
    public ImmutableArray<BudgetOverrunHold> ActiveOverrunHolds { get; }

    /// <inheritdoc/>
    public bool Equals(BudgetSnapshot? other) =>
        other is not null
        && ScopeId.Equals(other.ScopeId)
        && ObservedAt.Equals(other.ObservedAt)
        && Usages.SequenceEqual(other.Usages)
        && ActiveOverrunHolds.SequenceEqual(other.ActiveOverrunHolds);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ScopeId);
        hash.Add(ObservedAt);
        foreach (var usage in Usages)
        {
            hash.Add(usage);
        }
        foreach (var hold in ActiveOverrunHolds)
        {
            hash.Add(hold);
        }

        return hash.ToHashCode();
    }
}
