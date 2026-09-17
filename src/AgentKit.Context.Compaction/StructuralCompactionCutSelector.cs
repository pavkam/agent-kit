// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>
/// The built-in <see cref="ICompactionCutSelector"/>: chooses the largest
/// prefix of the eligible source snapshot that can be covered without
/// splitting a causal pairing and without leaving fewer than the requested
/// minimum number of retained entries, preferring a boundary immediately
/// before a user turn.
/// </summary>
/// <remarks>
/// <para>
/// A candidate boundary after the first <c>k</c> loaded entries is safe
/// only if no entry among the remaining, retained entries has a
/// <see cref="SessionEntry.CausalParentId"/> pointing into the covered
/// prefix — that would otherwise separate, for example, a tool call from
/// its terminal result. Safety is decided purely from
/// <see cref="SessionEntry.CausalParentId"/>; message content is never
/// inspected.
/// </para>
/// <para>
/// Among the safe boundaries, the selector walks down from the largest and
/// returns the first whose retained suffix begins with a
/// <see cref="MessageSessionEntry"/> carrying a <see cref="UserMessage"/>,
/// so the retained history starts at a complete turn rather than at an
/// assistant reply or tool result. Only when no user-turn boundary satisfies
/// the retention minimum does it fall back to the largest causally safe
/// boundary. The role check reads the message type, not its content.
/// </para>
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

        // The loaded source always starts at sequence 0 (causal-parent safety needs the full branch
        // prefix), so a long-lived branch that has already been compacted once permanently accumulates
        // more entries than any fixed ceiling. Counting only entries not yet covered by the newest
        // active compaction record against the ceiling keeps that record's already-summarized prefix
        // "free," so compaction remains possible for exactly the sessions that need it most instead of
        // being permanently rejected past MaximumSourceEntries.
        var uncoveredCount = entries.Length - CountEntriesCoveredByNewestActiveRecord(entries);
        if (uncoveredCount > _maximumSourceEntries)
        {
            return ValueTask.FromResult<CompactionCutSelectionResult>(new NoSafeCompactionCut(
                new CompactionRejection(
                    CompactionRejectionKind.SourceLimitExceeded,
                    $"The eligible source range carries {uncoveredCount} entries not yet covered by an earlier compaction, exceeding the configured maximum of {_maximumSourceEntries}.",
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

        var maximumCovered = entries.Length - minimumRetained;
        var causalBoundary = 0;
        for (var covered = maximumCovered; covered >= 1; covered--)
        {
            if (!IsSafeBoundary(entries, idPositions, covered))
            {
                continue;
            }

            // Prefer the largest safe boundary whose retained suffix begins at a user turn; remember the largest safe
            // boundary of any kind as the fallback when no user-turn boundary satisfies the retention minimum.
            if (BeginsUserTurn(entries, covered))
            {
                return ValueTask.FromResult<CompactionCutSelectionResult>(new CompactionCutSelected(BuildCut(entries, covered)));
            }

            if (causalBoundary == 0)
            {
                causalBoundary = covered;
            }
        }

        return causalBoundary > 0
            ? ValueTask.FromResult<CompactionCutSelectionResult>(new CompactionCutSelected(BuildCut(entries, causalBoundary)))
            : ValueTask.FromResult<CompactionCutSelectionResult>(new NoSafeCompactionCut(
            new CompactionRejection(
                CompactionRejectionKind.NoSafeCut,
                "No boundary was found that does not split a causal pairing.",
                ExtensionData.Empty)));
    }

    /// <summary>
    /// Counts how many of the loaded entries fall before the <see cref="CompactionManifest.RetainedSuffixStart"/>
    /// of the newest <see cref="CompactionRecordStatus.Active"/> <see cref="CompactionSessionEntry"/> in
    /// <paramref name="entries"/> - the prefix an earlier compaction already summarized, and therefore
    /// need not count against <see cref="_maximumSourceEntries"/> again.
    /// </summary>
    private static int CountEntriesCoveredByNewestActiveRecord(ImmutableArray<SessionEntry> entries)
    {
        for (var i = entries.Length - 1; i >= 0; i--)
        {
            if (entries[i] is not CompactionSessionEntry { Record.Status: CompactionRecordStatus.Active } active)
            {
                continue;
            }

            var retainedSuffixStart = active.Record.Manifest.RetainedSuffixStart.Value;
            var covered = 0;
            foreach (var entry in entries)
            {
                if (entry.Sequence.Value >= retainedSuffixStart)
                {
                    break;
                }

                covered++;
            }

            return covered;
        }

        return 0;
    }

    private static CompactionCut BuildCut(ImmutableArray<SessionEntry> entries, int covered)
    {
        Debug.Assert(covered >= 1 && covered <= entries.Length, "A boundary covers at least one loaded entry.");

        var coveredIds = entries.Take(covered).Select(static e => e.Id).ToImmutableArray();
        var retainedSuffixStart = covered < entries.Length
            ? entries[covered].Sequence
            : new SessionSequence(entries[covered - 1].Sequence.Value + 1);

        return new CompactionCut(
            new CompactionSourceRange(entries[0].Sequence, entries[covered - 1].Sequence),
            retainedSuffixStart,
            coveredIds);
    }

    private static bool BeginsUserTurn(ImmutableArray<SessionEntry> entries, int covered)
    {
        Debug.Assert(covered >= 1 && covered <= entries.Length, "A boundary covers at least one loaded entry.");

        return covered < entries.Length
            && entries[covered] is MessageSessionEntry { Message: UserMessage };
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
