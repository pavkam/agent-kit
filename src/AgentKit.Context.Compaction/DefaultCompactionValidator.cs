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
/// This validator never trusts <see cref="ICompactionCutSelector"/> or
/// <see cref="ICompactionStrategy"/> output implicitly; every candidate is
/// re-derived from <see cref="CompactionValidationRequest.Source"/> so a
/// defective selector or strategy cannot activate an unsafe checkpoint.
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
}
