// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports repeated contributions for one exact canonical tool identity.</summary>
/// <remarks>Conflicting sources, descriptors, or execution policies remain visible in the complete candidates. Equivalent overlapping toolsets also require explicit selection.</remarks>
public sealed record ToolCatalogIdentityCollision: ToolCatalogCollision
{
    /// <summary>Captures an ordered group of distinct contributions for the same identity.</summary>
    /// <param name="candidates">At least two nonnull, distinct candidates sharing one exact identity.</param>
    /// <exception cref="ArgumentException">The array is default, contains null, is too short, contains repeated candidates, or mixes identities.</exception>
    public ToolCatalogIdentityCollision(ImmutableArray<ToolCatalogCandidate> candidates)
    {
        ArgumentException.ThrowIfContainsNull(candidates);
        ArgumentException.ThrowIfNotEqual(candidates.Length >= 2, true, nameof(candidates));
        ArgumentException.ThrowIfNotEqual(candidates.Distinct().Count(), candidates.Length, nameof(candidates));
        ArgumentException.ThrowIfNotEqual(candidates.All(candidate => candidate.Identity == candidates[0].Identity), true, nameof(candidates));
        Candidates = candidates;
    }

    /// <summary>Gets every contribution competing for this identity.</summary>
    /// <value>Distinct candidates in deterministic catalog contribution order.</value>
    public ImmutableArray<ToolCatalogCandidate> Candidates { get; }

    /// <summary>Compares complete ordered collision evidence.</summary>
    /// <param name="other">The collision to compare, or null.</param>
    /// <returns>True when all candidates match in order.</returns>
    public bool Equals(ToolCatalogIdentityCollision? other) => other is not null && Candidates.SequenceEqual(other.Candidates);

    /// <summary>Hashes the complete ordered collision evidence.</summary>
    /// <returns>A hash consistent with structural equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var candidate in Candidates) { hash.Add(candidate); }
        return hash.ToHashCode();
    }
}
