// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records the disposition of one source in an assembled model request manifest.</summary>
public sealed record ContextManifestEntry
{
    /// <summary>Initializes one manifest entry.</summary>
    /// <param name="source">The exact source publication being described.</param>
    /// <param name="disposition">How the source was projected.</param>
    /// <param name="estimatedCost">The estimated cost attributed to this entry.</param>
    /// <param name="reason">A non-sensitive explanation when content was transformed or omitted.</param>
    /// <param name="sourceMessageIds">History message identifiers correlated with this entry, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="disposition"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is blank, or <paramref name="sourceMessageIds"/> is a default array.
    /// </exception>
    public ContextManifestEntry(
        ContextSourceReference source,
        ContextManifestDisposition disposition,
        ContextCostEstimate estimatedCost,
        string reason,
        ImmutableArray<MessageId> sourceMessageIds)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfUndefined(disposition);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfDefault(sourceMessageIds);

        Source = source;
        Disposition = disposition;
        EstimatedCost = estimatedCost;
        Reason = reason;
        SourceMessageIds = sourceMessageIds;
    }

    /// <summary>Gets the exact source publication being described.</summary>
    public ContextSourceReference Source { get; }

    /// <summary>Gets how the source was projected.</summary>
    public ContextManifestDisposition Disposition { get; }

    /// <summary>Gets the estimated cost attributed to this entry.</summary>
    public ContextCostEstimate EstimatedCost { get; }

    /// <summary>Gets a non-sensitive explanation when content was transformed or omitted.</summary>
    public string Reason { get; }

    /// <summary>Gets history message identifiers correlated with this entry, if any.</summary>
    public ImmutableArray<MessageId> SourceMessageIds { get; }
}
