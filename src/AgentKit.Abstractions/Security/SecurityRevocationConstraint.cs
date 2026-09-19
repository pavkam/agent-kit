// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds a grant or approval to the exact revocation epoch and trigger set that can retire it.</summary>
public sealed record SecurityRevocationConstraint
{
    /// <summary>Initializes a revocation constraint.</summary>
    /// <param name="version">The captured revocation epoch.</param>
    /// <param name="triggers">The ordered non-empty set of triggers that retire this epoch.</param>
    /// <exception cref="ArgumentException"><paramref name="triggers"/> is default or empty.</exception>
    public SecurityRevocationConstraint(SecurityRevocationVersion version, ImmutableArray<SecurityRevocationTrigger> triggers)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(triggers);
        Version = version;
        Triggers = triggers;
    }

    /// <summary>Gets the captured revocation epoch.</summary>
    public SecurityRevocationVersion Version { get; }
    /// <summary>Gets the ordered non-empty trigger set.</summary>
    public ImmutableArray<SecurityRevocationTrigger> Triggers { get; }

    /// <summary>Compares the epoch and the ordered trigger sequence.</summary>
    /// <param name="other">The constraint to compare with.</param>
    /// <returns><see langword="true"/> when both constraints bind the same epoch and triggers.</returns>
    public bool Equals(SecurityRevocationConstraint? other) =>
        other is not null && Version == other.Version && Triggers.SequenceEqual(other.Triggers);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Version);
        foreach (var trigger in Triggers)
        {
            hash.Add(trigger);
        }

        return hash.ToHashCode();
    }
}
