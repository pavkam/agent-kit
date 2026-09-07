// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One forward page of session entries, in sequence order.</summary>
public sealed record SessionPage: SessionPageResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionPage"/> record.</summary>
    /// <param name="entries">The entries in this page, in ascending sequence order.</param>
    /// <param name="throughSequence">
    /// The sequence of the last entry in this page, or the request's
    /// <c>FromSequenceExclusive</c> when the page is empty. Passing this
    /// value as the next request's <c>FromSequenceExclusive</c> continues
    /// the read.
    /// </param>
    /// <param name="hasMore">
    /// <see langword="true"/> if entries exist beyond
    /// <paramref name="throughSequence"/> at the time of this read.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> is a default, uninitialized array.
    /// </exception>
    public SessionPage(ImmutableArray<SessionEntry> entries, SessionSequence throughSequence, bool hasMore)
    {
        ArgumentException.ThrowIfDefault(entries);
        Entries = entries;
        ThroughSequence = throughSequence;
        HasMore = hasMore;
    }

    /// <summary>Gets the entries in this page, in ascending sequence order.</summary>
    public ImmutableArray<SessionEntry> Entries { get; init; }

    /// <summary>
    /// Gets the sequence of the last entry in this page, or the request's
    /// starting sequence when the page is empty.
    /// </summary>
    public SessionSequence ThroughSequence { get; init; }

    /// <summary>
    /// Gets a value indicating whether entries exist beyond
    /// <see cref="ThroughSequence"/> at the time of this read.
    /// </summary>
    public bool HasMore { get; init; }

    /// <inheritdoc/>
    public bool Equals(SessionPage? other) =>
        other is not null
        && Entries.SequenceEqual(other.Entries)
        && ThroughSequence.Equals(other.ThroughSequence)
        && HasMore == other.HasMore;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in Entries)
        {
            hash.Add(entry);
        }

        hash.Add(ThroughSequence);
        hash.Add(HasMore);
        return hash.ToHashCode();
    }
}
