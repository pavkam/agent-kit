// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One specific problem found while validating a compaction candidate.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="SafeMessage"/> never contains source session content.
/// </remarks>
public sealed record CompactionValidationIssue
{
    /// <summary>Initializes a new instance of the <see cref="CompactionValidationIssue"/> record.</summary>
    /// <param name="kind">The category of this issue.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="sourceEntryIds">The source entries implicated by this issue, if any.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace, or <paramref name="sourceEntryIds"/> is a default,
    /// uninitialized array.
    /// </exception>
    public CompactionValidationIssue(
        CompactionValidationIssueKind kind, string safeMessage, ImmutableArray<SessionEntryId> sourceEntryIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentException.ThrowIfDefault(sourceEntryIds);

        Kind = kind;
        SafeMessage = safeMessage;
        SourceEntryIds = sourceEntryIds;
    }

    /// <summary>Gets the category of this issue.</summary>
    public CompactionValidationIssueKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>Gets the source entries implicated by this issue, if any.</summary>
    public ImmutableArray<SessionEntryId> SourceEntryIds { get; init; }

    /// <inheritdoc/>
    public bool Equals(CompactionValidationIssue? other) =>
        other is not null
        && Kind == other.Kind
        && SafeMessage == other.SafeMessage
        && SourceEntryIds.SequenceEqual(other.SourceEntryIds);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Kind);
        hash.Add(SafeMessage);
        foreach (var id in SourceEntryIds)
        {
            hash.Add(id);
        }

        return hash.ToHashCode();
    }
}
