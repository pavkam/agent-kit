// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the merged, deterministic capture of every provider's targets for one discovery request.</summary>
/// <remarks>Targets appear in provider registration order and each provider's own order. The same agent published by two providers is ambiguous and is left for the selector to resolve or reject; the catalog never picks a winner.</remarks>
public sealed record DelegationTargetCatalogSnapshot
{
    /// <summary>Initializes a validated snapshot.</summary>
    /// <param name="targets">The merged targets; empty when none.</param>
    /// <exception cref="ArgumentException"><paramref name="targets"/> is default or contains null.</exception>
    public DelegationTargetCatalogSnapshot(ImmutableArray<DelegationTarget> targets)
    {
        ArgumentException.ThrowIfDefault(targets);
        ArgumentException.ThrowIfContainsNull(targets);
        Targets = targets;
    }

    /// <summary>Gets the merged targets.</summary>
    public ImmutableArray<DelegationTarget> Targets { get; }

    /// <inheritdoc/>
    public bool Equals(DelegationTargetCatalogSnapshot? other) => other is not null && Targets.SequenceEqual(other.Targets);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var target in Targets)
        {
            hash.Add(target);
        }

        return hash.ToHashCode();
    }
}
