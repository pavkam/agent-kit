// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete semantic cut selected over a source snapshot: exactly which
/// entries a candidate would cover, and where the untouched retained suffix
/// begins.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. A cut can never split an assistant part, a tool
/// call from its terminal result, or an admitted input from its promotion.
/// The first-party selector enforces this through recorded causal parents and
/// prefers a boundary whose retained suffix begins at a user turn, falling
/// back to the largest causally safe boundary only when no user-turn boundary
/// satisfies the retention minimum; the validator independently re-derives
/// <see cref="CoveredEntryIds"/>, <see cref="CoveredRange"/>, and
/// <see cref="RetainedSuffixStart"/> from the source before activation.
/// </remarks>
public sealed record CompactionCut
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCut"/> record.</summary>
    /// <param name="coveredRange">The inclusive range of sequences this cut covers.</param>
    /// <param name="retainedSuffixStart">The first sequence of the untouched retained suffix.</param>
    /// <param name="coveredEntryIds">The exact entries covered by <paramref name="coveredRange"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="coveredRange"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="coveredEntryIds"/> is a default, uninitialized array.
    /// </exception>
    public CompactionCut(
        CompactionSourceRange coveredRange,
        SessionSequence retainedSuffixStart,
        ImmutableArray<SessionEntryId> coveredEntryIds)
    {
        ArgumentNullException.ThrowIfNull(coveredRange);
        ArgumentException.ThrowIfDefault(coveredEntryIds);

        CoveredRange = coveredRange;
        RetainedSuffixStart = retainedSuffixStart;
        CoveredEntryIds = coveredEntryIds;
    }

    /// <summary>Gets the inclusive range of sequences this cut covers.</summary>
    public CompactionSourceRange CoveredRange { get; init; }

    /// <summary>Gets the first sequence of the untouched retained suffix.</summary>
    public SessionSequence RetainedSuffixStart { get; init; }

    /// <summary>Gets the exact entries covered by <see cref="CoveredRange"/>.</summary>
    public ImmutableArray<SessionEntryId> CoveredEntryIds { get; init; }

    /// <inheritdoc/>
    public bool Equals(CompactionCut? other) =>
        other is not null
        && CoveredRange.Equals(other.CoveredRange)
        && RetainedSuffixStart.Equals(other.RetainedSuffixStart)
        && CoveredEntryIds.SequenceEqual(other.CoveredEntryIds);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CoveredRange);
        hash.Add(RetainedSuffixStart);
        foreach (var id in CoveredEntryIds)
        {
            hash.Add(id);
        }

        return hash.ToHashCode();
    }
}
