// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one provider's independent capture of discoverable delegation targets.</summary>
public sealed record DelegationTargetSnapshot
{
    /// <summary>Initializes a validated snapshot.</summary>
    /// <param name="source">The provider that produced the capture.</param>
    /// <param name="targets">The discovered targets in provider order; empty when none.</param>
    /// <exception cref="ArgumentException"><paramref name="source"/> is blank, or <paramref name="targets"/> is default or contains null.</exception>
    public DelegationTargetSnapshot(ComponentId source, ImmutableArray<DelegationTarget> targets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Value, nameof(source));
        ArgumentException.ThrowIfDefault(targets);
        ArgumentException.ThrowIfContainsNull(targets);
        Source = source;
        Targets = targets;
    }

    /// <summary>Gets the provider that produced the capture.</summary>
    public ComponentId Source { get; }

    /// <summary>Gets the discovered targets in provider order.</summary>
    public ImmutableArray<DelegationTarget> Targets { get; }

    /// <inheritdoc/>
    public bool Equals(DelegationTargetSnapshot? other) => other is not null && Source == other.Source && Targets.SequenceEqual(other.Targets);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source);
        foreach (var target in Targets)
        {
            hash.Add(target);
        }

        return hash.ToHashCode();
    }
}
