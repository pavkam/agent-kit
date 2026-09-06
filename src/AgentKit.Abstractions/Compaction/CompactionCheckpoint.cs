// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The content produced by a compaction strategy: a summary that stands in
/// for the covered source entries in future context assembly.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller checkpoint shape
/// described by the context-compaction architecture, which additionally
/// carries typed references to durable extracted state (open goals,
/// decisions, pending tool effects) alongside the prose summary. Until a
/// dedicated state-extraction contract exists, that structured state is
/// represented, if at all, inside <see cref="Extensions"/>.
/// </para>
/// </remarks>
public sealed record CompactionCheckpoint
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCheckpoint"/> record.</summary>
    /// <param name="summary">The summary content parts standing in for the covered entries.</param>
    /// <param name="extensions">Caller-specific or forward-compatible checkpoint data.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="summary"/> is a default, uninitialized array, or is
    /// empty.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public CompactionCheckpoint(ImmutableArray<ContentPart> summary, ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(summary);
        if (summary.IsEmpty)
        {
            throw new ArgumentException("Summary must contain at least one content part.", nameof(summary));
        }

        ArgumentNullException.ThrowIfNull(extensions);

        Summary = summary;
        Extensions = extensions;
    }

    /// <summary>Gets the summary content parts standing in for the covered entries.</summary>
    public ImmutableArray<ContentPart> Summary { get; init; }

    /// <summary>Gets caller-specific or forward-compatible checkpoint data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(CompactionCheckpoint? other) =>
        other is not null && Summary.SequenceEqual(other.Summary) && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var part in Summary)
        {
            hash.Add(part);
        }

        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
