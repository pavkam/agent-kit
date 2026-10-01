// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Counts what a retrieval searched and why candidates were left out, with no content.</summary>
/// <remarks>The counts make omission auditable without exposing what was omitted. Every count is nonnegative.</remarks>
public sealed record RetrievalSummary
{
    /// <summary>Initializes a validated summary.</summary>
    /// <param name="searched">The number of candidates produced by sources before filtering.</param>
    /// <param name="omittedStale">The candidates dropped because authoritative state no longer contains them.</param>
    /// <param name="omittedUnauthorized">The candidates dropped by classification or exposure authorization.</param>
    /// <param name="omittedDuplicate">The candidates dropped as duplicates of a better-ranked candidate.</param>
    /// <param name="omittedByBudget">The candidates dropped by the item, byte, or token budget.</param>
    /// <param name="sourcesUnavailable">The selected sources that failed or were denied.</param>
    /// <param name="deletionGeneration">The highest deletion generation the exposure decision bound, or <see langword="null"/> when no store reported one.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or the generation is negative.</exception>
    public RetrievalSummary(
        int searched,
        int omittedStale,
        int omittedUnauthorized,
        int omittedDuplicate,
        int omittedByBudget,
        int sourcesUnavailable,
        long? deletionGeneration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(searched);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedStale);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedUnauthorized);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedDuplicate);
        ArgumentOutOfRangeException.ThrowIfNegative(omittedByBudget);
        ArgumentOutOfRangeException.ThrowIfNegative(sourcesUnavailable);
        if (deletionGeneration is { } generation)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(generation, nameof(deletionGeneration));
        }

        Searched = searched;
        OmittedStale = omittedStale;
        OmittedUnauthorized = omittedUnauthorized;
        OmittedDuplicate = omittedDuplicate;
        OmittedByBudget = omittedByBudget;
        SourcesUnavailable = sourcesUnavailable;
        DeletionGeneration = deletionGeneration;
    }

    /// <summary>Gets the candidates produced by sources before filtering.</summary>
    public int Searched { get; }

    /// <summary>Gets the candidates dropped because authoritative state no longer contains them.</summary>
    public int OmittedStale { get; }

    /// <summary>Gets the candidates dropped by classification or exposure authorization.</summary>
    public int OmittedUnauthorized { get; }

    /// <summary>Gets the candidates dropped as duplicates.</summary>
    public int OmittedDuplicate { get; }

    /// <summary>Gets the candidates dropped by the budget.</summary>
    public int OmittedByBudget { get; }

    /// <summary>Gets the selected sources that failed or were denied.</summary>
    public int SourcesUnavailable { get; }

    /// <summary>Gets the highest deletion generation the exposure decision bound, or <see langword="null"/>.</summary>
    public long? DeletionGeneration { get; }
}
