// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>
/// The built-in <see cref="ICompactionCutSelector"/>: chooses the largest
/// prefix of the eligible source snapshot that can be covered without
/// splitting a causal pairing and without leaving fewer than the requested
/// minimum number of retained entries.
/// </summary>
/// <remarks>
/// This selector reasons purely over each entry's recorded
/// <see cref="SessionEntry.Sequence"/> and <see cref="SessionEntry.CausalParentId"/>;
/// it never inspects message content. A candidate boundary after the first
/// <c>k</c> loaded entries is safe only if no entry among the remaining,
/// retained entries has a <see cref="SessionEntry.CausalParentId"/> pointing
/// into the covered prefix — that would otherwise separate, for example, a
/// tool call from its terminal result.
/// </remarks>
public sealed class StructuralCompactionCutSelector: ICompactionCutSelector
{
    private readonly int _maximumSourceEntries;

    /// <summary>Initializes a new instance of the <see cref="StructuralCompactionCutSelector"/> class.</summary>
    /// <param name="options">The validated compaction options carrying the source-size ceiling.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public StructuralCompactionCutSelector(IOptions<CompactionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maximumSourceEntries = options.Value.MaximumSourceEntries;
    }

    /// <inheritdoc/>
    public ValueTask<CompactionCutSelectionResult> SelectAsync(
        CompactionCutSelectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var entries = request.Source.Entries;

        if (entries.Length > _maximumSourceEntries)
        {
            return ValueTask.FromResult<CompactionCutSelectionResult>(new NoSafeCompactionCut(
                new CompactionRejection(
                    CompactionRejectionKind.SourceLimitExceeded,
                    $"The eligible source range carries {entries.Length} entries, exceeding the configured maximum of {_maximumSourceEntries}.",
                    ExtensionData.Empty)));
        }

        var minimumRetained = Math.Max(request.Request.MinimumRetainedEntries, 0);
        if (entries.Length <= minimumRetained)
        {
            return ValueTask.FromResult<CompactionCutSelectionResult>(new NoSafeCompactionCut(
                new CompactionRejection(
                    CompactionRejectionKind.NoSafeCut,
                    "The eligible source range does not exceed the minimum number of entries that must remain retained.",
                    ExtensionData.Empty)));
        }

        var idPositions = new Dictionary<SessionEntryId, int>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            idPositions[entries[i].Id] = i;
        }

        for (var covered = entries.Length - minimumRetained; covered >= 1; covered--)
        {
            if (IsSafeBoundary(entries, idPositions, covered))
            {
                var coveredIds = entries.Take(covered).Select(static e => e.Id).ToImmutableArray();
                var retainedSuffixStart = covered < entries.Length
                    ? entries[covered].Sequence
                    : new SessionSequence(entries[covered - 1].Sequence.Value + 1);
                var cut = new CompactionCut(
                    new CompactionSourceRange(entries[0].Sequence, entries[covered - 1].Sequence),
                    retainedSuffixStart,
                    coveredIds);

                return ValueTask.FromResult<CompactionCutSelectionResult>(new CompactionCutSelected(cut));
            }
        }

        return ValueTask.FromResult<CompactionCutSelectionResult>(new NoSafeCompactionCut(
            new CompactionRejection(
                CompactionRejectionKind.NoSafeCut,
                "No boundary was found that does not split a causal pairing.",
                ExtensionData.Empty)));
    }

    private static bool IsSafeBoundary(
        ImmutableArray<SessionEntry> entries, Dictionary<SessionEntryId, int> idPositions, int covered)
    {
        for (var i = covered; i < entries.Length; i++)
        {
            var parentId = entries[i].CausalParentId;
            if (parentId is { } id && idPositions.TryGetValue(id, out var parentIndex) && parentIndex < covered)
            {
                return false;
            }
        }

        return true;
    }
}
