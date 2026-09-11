// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports multiple explicit contributions advertising the same provider-visible alias.</summary>
/// <remarks>Resolved candidates and missing targets retain their exact authored assignments. Textual equality with a tool ID is irrelevant, and an unresolved assignment cannot disappear from collision evidence.</remarks>
public sealed record ToolCatalogAliasCollision: ToolCatalogCollision
{
    /// <summary>Captures every competing binding for one authored alias.</summary>
    /// <param name="alias">The nondefault exact provider-visible alias.</param>
    /// <param name="candidates">At least two distinct, nonnull candidates explicitly assigned this alias.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alias"/> is default.</exception>
    /// <exception cref="ArgumentException">The array is default, contains null, is too short, repeats candidates, or contains a candidate without the alias.</exception>
    public ToolCatalogAliasCollision(ToolAlias alias, ImmutableArray<ToolCatalogCandidate> candidates)
        : this(alias, candidates, []) { }

    /// <summary>Captures all resolved and unresolved contributions competing for one authored alias.</summary>
    /// <param name="alias">The nondefault exact provider-visible alias.</param>
    /// <param name="candidates">Initialized, distinct, nonnull resolved candidates explicitly assigned this alias.</param>
    /// <param name="missingTargets">Initialized, distinct, nonnull unresolved assignments of this alias; resolved and unresolved contributions total at least two.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alias"/> is default.</exception>
    /// <exception cref="ArgumentException">An array is default, contains null or duplicates, has mismatched alias evidence, or the combined contribution count is below two.</exception>
    public ToolCatalogAliasCollision(ToolAlias alias, ImmutableArray<ToolCatalogCandidate> candidates, ImmutableArray<ToolCatalogMissingAliasTarget> missingTargets)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(alias, default);
        ArgumentException.ThrowIfContainsNull(candidates);
        ArgumentException.ThrowIfContainsNull(missingTargets);
        ArgumentException.ThrowIfNotEqual((long) candidates.Length + missingTargets.Length >= 2, true, nameof(candidates));
        ArgumentException.ThrowIfNotEqual(candidates.Distinct().Count(), candidates.Length, nameof(candidates));
        ArgumentException.ThrowIfNotEqual(candidates.All(candidate => candidate.Toolset.Aliases.Any(assignment => assignment.Alias == alias && assignment.Tool == candidate.Identity)), true, nameof(candidates));
        ArgumentException.ThrowIfNotEqual(missingTargets.Distinct().Count(), missingTargets.Length, nameof(missingTargets));
        ArgumentException.ThrowIfNotEqual(missingTargets.All(target => target.Assignment.Alias == alias), true, nameof(missingTargets));
        Alias = alias;
        Candidates = candidates;
        MissingTargets = missingTargets;
    }

    /// <summary>Gets the ambiguous provider-visible alias.</summary>
    /// <value>The exact authored alias, never inferred from a descriptor.</value>
    public ToolAlias Alias { get; }

    /// <summary>Gets every contribution advertising the alias.</summary>
    /// <value>Distinct candidates in deterministic catalog contribution order.</value>
    public ImmutableArray<ToolCatalogCandidate> Candidates { get; }

    /// <summary>Gets unresolved assignments that still contribute to this alias collision.</summary>
    /// <value>An initialized immutable array in authored order; empty when every contribution has a descriptor.</value>
    public ImmutableArray<ToolCatalogMissingAliasTarget> MissingTargets { get; }

    /// <summary>Compares the alias and complete ordered competing evidence.</summary>
    /// <param name="other">The collision to compare, or null.</param>
    /// <returns>True when the alias and candidates match.</returns>
    public bool Equals(ToolCatalogAliasCollision? other) => other is not null && Alias == other.Alias && Candidates.SequenceEqual(other.Candidates) && MissingTargets.SequenceEqual(other.MissingTargets);

    /// <summary>Hashes the alias and complete ordered evidence.</summary>
    /// <returns>A hash consistent with structural equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Alias);
        foreach (var candidate in Candidates) { hash.Add(candidate); }
        foreach (var missing in MissingTargets) { hash.Add(missing); }
        return hash.ToHashCode();
    }
}
