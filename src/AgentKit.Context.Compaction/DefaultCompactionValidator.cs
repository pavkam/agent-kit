// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>
/// The built-in <see cref="ICompactionValidator"/>: independently re-checks
/// structural soundness, causal safety, content boundedness, and measurable
/// size reduction before a candidate may be activated.
/// </summary>
/// <remarks>
/// <para>
/// This validator never trusts <see cref="ICompactionCutSelector"/> or
/// <see cref="ICompactionStrategy"/> output implicitly; every candidate is
/// re-derived from <see cref="CompactionValidationRequest.Source"/> so a
/// defective selector or strategy cannot activate an unsafe checkpoint.
/// </para>
/// <para>
/// Structural coherence is checked before causality and size: the covered
/// identities must be exactly the ordered, duplicate-free source prefix of
/// their length; the covered range must span the first and last covered
/// sequences; the retained suffix must begin at the first retained entry
/// (or one past the last covered sequence) and strictly after the range;
/// and the manifest's branch, source version, operation context, context
/// epoch, covered range, and retained suffix start must agree with the
/// request, source, and cut. Each violation is reported as
/// <see cref="CompactionValidationIssueKind.InvalidStructure"/>.
/// </para>
/// </remarks>
public sealed class DefaultCompactionValidator: ICompactionValidator
{
    private readonly ICompactionSizeEstimator _estimator;
    private readonly int _maximumCheckpointCharacters;

    /// <summary>Initializes a new instance of the <see cref="DefaultCompactionValidator"/> class.</summary>
    /// <param name="estimator">The size estimator used to check the reduction ratio.</param>
    /// <param name="options">The validated compaction options carrying the checkpoint character ceiling.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="estimator"/> or <paramref name="options"/> is null.
    /// </exception>
    public DefaultCompactionValidator(ICompactionSizeEstimator estimator, IOptions<CompactionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(options);

        _estimator = estimator;
        _maximumCheckpointCharacters = options.Value.MaximumCheckpointCharacters;
    }

    /// <inheritdoc/>
    public ValueTask<CompactionValidationResult> ValidateAsync(
        CompactionValidationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<CompactionValidationIssue>();
        var checkpoint = request.Candidate.Checkpoint;

        var extract = ContentTextExtractor.ExtractPartsText(checkpoint.Summary);
        if (string.IsNullOrWhiteSpace(extract))
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure,
                "The candidate checkpoint carries no meaningful text.",
                []));
        }

        if (extract.Length > _maximumCheckpointCharacters)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.UnboundedContent,
                $"The candidate checkpoint carries {extract.Length} characters, exceeding the configured maximum of {_maximumCheckpointCharacters}.",
                []));
        }

        var sourceIds = request.Source.Entries.ToImmutableDictionary(static e => e.Id, static e => e);
        var missing = request.Cut.CoveredEntryIds.Where(id => !sourceIds.ContainsKey(id)).ToImmutableArray();
        if (!missing.IsEmpty)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.MissingSource,
                "The candidate references covered entries not present in the source snapshot.",
                missing));
        }

        AddCutCoherenceIssues(issues, request.Source, request.Cut);
        AddManifestCoherenceIssues(issues, request.Request, request.Source, request.Cut, request.Candidate.Manifest);

        var coveredIds = request.Cut.CoveredEntryIds.ToImmutableHashSet();
        var broken = request.Source.Entries
            .Where(e => !coveredIds.Contains(e.Id) && e.CausalParentId is { } parentId && coveredIds.Contains(parentId))
            .Select(static e => e.Id)
            .ToImmutableArray();
        if (!broken.IsEmpty)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.BrokenCausality,
                "The candidate's cut separates a retained entry from a covered causal parent.",
                broken));
        }

        var before = _estimator.EstimateEntries(
            [.. request.Source.Entries.Where(e => coveredIds.Contains(e.Id))]);
        var after = _estimator.EstimateCheckpoint(checkpoint);
        var achievedRatio = before.Tokens == 0 ? 0d : 1d - ((double) after.Tokens / before.Tokens);
        if (achievedRatio < request.Request.MinimumReductionRatio)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.NonReducing,
                $"The candidate reduces estimated size by {achievedRatio:P1}, below the required {request.Request.MinimumReductionRatio:P1}.",
                []));
        }

        CompactionValidationResult result = issues.Count > 0
            ? new CompactionValidationRejected([.. issues])
            : new CompactionValidated(request.Candidate);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Re-derives the cut from the source and rejects any cut whose covered identities, covered range, or retained
    /// suffix start disagree with the snapshot: covered identities must be exactly the source prefix of their
    /// length in order without duplicates, the range must span the first and last covered sequences, and the
    /// retained suffix must begin at the first retained entry (or one past the last covered sequence when nothing
    /// is retained) and strictly after the covered range.
    /// </summary>
    private static void AddCutCoherenceIssues(
        List<CompactionValidationIssue> issues, CompactionSourceSnapshot source, CompactionCut cut)
    {
        Debug.Assert(issues is not null, "The caller owns the issue list.");
        Debug.Assert(source is not null && cut is not null, "The request validated its members.");

        var covered = cut.CoveredEntryIds;
        if (covered.IsEmpty)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure,
                "The cut covers no entries.",
                []));
            return;
        }

        var entries = source.Entries;
        var prefixMatches = covered.Length <= entries.Length;
        for (var i = 0; prefixMatches && i < covered.Length; i++)
        {
            prefixMatches = entries[i].Id == covered[i];
        }

        if (!prefixMatches)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure,
                "The covered entry identities are not the contiguous, ordered, duplicate-free prefix of the source snapshot.",
                covered));

            // Range and suffix checks below assume the prefix shape; report the one structural fault and stop.
            return;
        }

        var firstCovered = entries[0].Sequence;
        var lastCovered = entries[covered.Length - 1].Sequence;
        if (cut.CoveredRange.StartInclusive != firstCovered || cut.CoveredRange.EndInclusive != lastCovered)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure,
                "The covered range does not span exactly the first and last covered sequences.",
                []));
        }

        var expectedRetainedStart = covered.Length < entries.Length
            ? entries[covered.Length].Sequence
            : new SessionSequence(lastCovered.Value + 1);
        if (cut.RetainedSuffixStart != expectedRetainedStart || cut.RetainedSuffixStart.Value <= lastCovered.Value)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure,
                "The retained suffix start does not follow the covered range at the first retained entry.",
                []));
        }
    }

    /// <summary>
    /// Rejects a manifest whose identity or coverage claims disagree with the request, the source snapshot, or the
    /// cut, so a strategy or caller cannot persist a record that describes a different branch, version, epoch,
    /// or range than the one actually validated.
    /// </summary>
    private static void AddManifestCoherenceIssues(
        List<CompactionValidationIssue> issues,
        CompactionRequest request,
        CompactionSourceSnapshot source,
        CompactionCut cut,
        CompactionManifest manifest)
    {
        Debug.Assert(issues is not null, "The caller owns the issue list.");
        Debug.Assert(request is not null && source is not null && cut is not null && manifest is not null, "The request validated its members.");

        var mismatches = new List<string>(6);
        if (manifest.BranchId != source.BranchId)
        {
            mismatches.Add("branch");
        }

        if (manifest.SourceVersion != source.Version)
        {
            mismatches.Add("source version");
        }

        if (!manifest.Context.Equals(source.Context))
        {
            mismatches.Add("operation context");
        }

        if (manifest.ContextEpoch != request.ContextEpoch)
        {
            mismatches.Add("context epoch");
        }

        if (!manifest.CoveredRange.Equals(cut.CoveredRange))
        {
            mismatches.Add("covered range");
        }

        if (manifest.RetainedSuffixStart != cut.RetainedSuffixStart)
        {
            mismatches.Add("retained suffix start");
        }

        if (mismatches.Count > 0)
        {
            issues.Add(new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure,
                $"The manifest disagrees with the validated source and cut on: {string.Join(", ", mismatches)}.",
                []));
        }
    }
}
