// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States what a delegated result must satisfy before its parent may accept it.</summary>
/// <remarks>Criteria are instructions to the child and inputs to the parent's validation; they never grant authority. When <see cref="RequiresEvidence"/> is set, a result without at least one <see cref="EvidenceReference"/> cannot be accepted.</remarks>
public sealed record AcceptanceCriteria
{
    /// <summary>Initializes validated acceptance criteria.</summary>
    /// <param name="criteria">The ordered, non-blank criteria; empty when the objective alone defines success.</param>
    /// <param name="requiresEvidence"><see langword="true"/> when an accepted result must carry verifiable evidence.</param>
    /// <exception cref="ArgumentException"><paramref name="criteria"/> is default or contains a blank entry.</exception>
    /// <exception cref="ArgumentNullException">An entry is null.</exception>
    public AcceptanceCriteria(ImmutableArray<string> criteria, bool requiresEvidence)
    {
        ArgumentException.ThrowIfDefault(criteria);
        foreach (var criterion in criteria)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(criterion, nameof(criteria));
        }

        Criteria = criteria;
        RequiresEvidence = requiresEvidence;
    }

    /// <summary>Gets the ordered criteria.</summary>
    public ImmutableArray<string> Criteria { get; }

    /// <summary>Gets a value indicating whether an accepted result must carry evidence.</summary>
    public bool RequiresEvidence { get; }

    /// <inheritdoc/>
    public bool Equals(AcceptanceCriteria? other) =>
        other is not null && RequiresEvidence == other.RequiresEvidence && Criteria.SequenceEqual(other.Criteria);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RequiresEvidence);
        foreach (var criterion in Criteria)
        {
            hash.Add(criterion);
        }

        return hash.ToHashCode();
    }
}
