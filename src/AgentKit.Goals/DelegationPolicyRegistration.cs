// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Declares the stable identity and ordering constraints of one additive delegation policy.</summary>
/// <remarks>A goal profile selects policies by <see cref="Id"/>. Constraints are soft: a policy listed in <see cref="After"/> that the profile does not select is ignored, and a cycle among the selected policies fails composition validation.</remarks>
public sealed record DelegationPolicyRegistration
{
    /// <summary>Initializes a validated registration.</summary>
    /// <param name="id">The non-blank policy identity.</param>
    /// <param name="after">Policies that must run before this one; empty when none.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> or an entry is blank, <paramref name="after"/> is default or repeats an entry, or it names <paramref name="id"/> itself.</exception>
    public DelegationPolicyRegistration(ComponentId id, ImmutableArray<ComponentId> after = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        var declared = after.IsDefault ? [] : after;
        var seen = new HashSet<ComponentId>();
        foreach (var predecessor in declared)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(predecessor.Value, nameof(after));
            if (predecessor == id || !seen.Add(predecessor))
            {
                throw new ArgumentException("Policy ordering constraints must be unique and must not name the policy itself.", nameof(after));
            }
        }

        Id = id;
        After = declared;
    }

    /// <summary>Gets the policy identity.</summary>
    public ComponentId Id { get; }

    /// <summary>Gets the policies that must run before this one.</summary>
    public ImmutableArray<ComponentId> After { get; }

    /// <inheritdoc/>
    public bool Equals(DelegationPolicyRegistration? other) => other is not null && Id == other.Id && After.SequenceEqual(other.After);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        foreach (var predecessor in After)
        {
            hash.Add(predecessor);
        }

        return hash.ToHashCode();
    }
}
