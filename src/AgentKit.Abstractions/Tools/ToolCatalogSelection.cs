// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects exact candidate evidence for every canonical identity and explicit provider alias.</summary>
/// <remarks>Construction validates local uniqueness and authored aliases. The consuming catalog additionally proves completeness, graph membership, and alias/descriptor/policy coherence. Supplied order cannot reorder the catalog.</remarks>
public sealed record ToolCatalogSelection: ToolCatalogMergeDecision
{
    /// <summary>Captures a complete proposed choice without acquiring or altering a binding.</summary>
    /// <param name="tools">Initialized, nonnull candidates with unique exact tool identities.</param>
    /// <param name="aliases">Nonnull explicit alias assignments to candidates; comparers cannot weaken domain equality.</param>
    /// <exception cref="ArgumentNullException"><paramref name="aliases"/> or an alias-map candidate is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An alias key is default.</exception>
    /// <exception cref="ArgumentException">The tools array is default or contains null, identities repeat, alias normalization collides, or an alias is absent from its candidate's authored mappings.</exception>
    public ToolCatalogSelection(ImmutableArray<ToolCatalogCandidate> tools, ImmutableDictionary<ToolAlias, ToolCatalogCandidate> aliases)
    {
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentException.ThrowIfNotEqual(tools.Select(static candidate => candidate.Identity).Distinct().Count(), tools.Length, nameof(tools));
        var captured = ImmutableDictionary.CreateBuilder<ToolAlias, ToolCatalogCandidate>();
        foreach (var pair in aliases)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(pair.Key, default, nameof(aliases));
            ArgumentNullException.ThrowIfNull(pair.Value, nameof(aliases));
            ArgumentException.ThrowIfNotEqual(pair.Value.Toolset.Aliases.Any(assignment => assignment.Alias == pair.Key && assignment.Tool == pair.Value.Identity), true, nameof(aliases));
            ArgumentException.ThrowIfNotEqual(captured.TryAdd(pair.Key, pair.Value), true, nameof(aliases));
        }
        Tools = tools;
        Aliases = captured.ToImmutable();
    }

    /// <summary>Gets the proposed selected contribution for each identity.</summary>
    /// <value>Unique exact identities; the catalog restores captured contribution order independently of this array.</value>
    public ImmutableArray<ToolCatalogCandidate> Tools { get; }

    /// <summary>Gets the exact authored alias contributions proposed for exposure.</summary>
    /// <value>A normalized immutable map, revalidated against the originating merge context.</value>
    public ImmutableDictionary<ToolAlias, ToolCatalogCandidate> Aliases { get; }

    /// <summary>Compares ordered selected tools and exact aliases independently of map enumeration order.</summary>
    /// <param name="other">The selection to compare, or null.</param>
    /// <returns>True when complete selected evidence matches.</returns>
    public bool Equals(ToolCatalogSelection? other) => other is not null && Tools.SequenceEqual(other.Tools)
        && Aliases.Count == other.Aliases.Count && Aliases.All(pair => other.Aliases.TryGetValue(pair.Key, out var candidate) && candidate == pair.Value);

    /// <summary>Hashes ordered tools and order-independent alias evidence.</summary>
    /// <returns>A structural hash consistent with equality.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var candidate in Tools) { hash.Add(candidate); }
        var aliases = 0;
        foreach (var pair in Aliases) { aliases ^= HashCode.Combine(pair.Key, pair.Value); }
        hash.Add(aliases);
        hash.Add(Aliases.Count);
        return hash.ToHashCode();
    }
}
