// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>
/// The default <see cref="ICompactor"/>: loads the eligible source range
/// from <see cref="ISessionCoordinator"/>, selects a cut, produces a
/// checkpoint, validates the candidate, and activates it through a
/// version-checked append.
/// </summary>
/// <remarks>
/// This class composes exactly one <see cref="ICompactionCutSelector"/>,
/// one <see cref="ICompactionStrategy"/>, and one
/// <see cref="ICompactionValidator"/>; it never re-implements their
/// responsibilities inline. Activation reuses the documented invariant that
/// a branch's <see cref="SessionVersion"/> equals its committed entry
/// count: appending exactly one <see cref="CompactionSessionEntry"/> against
/// an expected version of <c>V</c> deterministically advances the branch to
/// <c>V + 1</c>, so this compactor computes the activated version before
/// calling <see cref="ISessionCoordinator.AppendAsync"/> rather than relying
/// on a value it does not yet have.
/// </remarks>
public sealed class DefaultCompactor: ICompactor
{
    private readonly ISessionCoordinator _coordinator;
    private readonly ICompactionCutSelector _cutSelector;
    private readonly ICompactionStrategy _strategy;
    private readonly ICompactionValidator _validator;
    private readonly ICompactionSizeEstimator _estimator;
    private readonly IIdentifierGenerator<CompactionManifestId> _manifestIds;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly TimeProvider _timeProvider;
    private readonly int _sourceReadPageSize;
    private readonly ILogger<DefaultCompactor> _logger;

    /// <summary>Initializes a new instance of the <see cref="DefaultCompactor"/> class.</summary>
    /// <param name="coordinator">The session coordinator used to load source entries and activate a candidate.</param>
    /// <param name="cutSelector">The cut selector used to choose a structurally safe boundary.</param>
    /// <param name="strategy">The strategy used to produce checkpoint content.</param>
    /// <param name="validator">The validator used to check a produced candidate before activation.</param>
    /// <param name="estimator">The size estimator used to compute manifest before/after sizes.</param>
    /// <param name="manifestIds">Generates identities for produced manifests.</param>
    /// <param name="entryIds">Generates identities for the appended compaction entry.</param>
    /// <param name="timeProvider">The clock used to timestamp produced manifests and records.</param>
    /// <param name="options">The validated compaction options carrying the source read page size.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public DefaultCompactor(
        ISessionCoordinator coordinator,
        ICompactionCutSelector cutSelector,
        ICompactionStrategy strategy,
        ICompactionValidator validator,
        ICompactionSizeEstimator estimator,
        IIdentifierGenerator<CompactionManifestId> manifestIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider,
        IOptions<CompactionOptions> options,
        ILogger<DefaultCompactor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(cutSelector);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(manifestIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _coordinator = coordinator;
        _cutSelector = cutSelector;
        _strategy = strategy;
        _validator = validator;
        _estimator = estimator;
        _manifestIds = manifestIds;
        _entryIds = entryIds;
        _timeProvider = timeProvider;
        _sourceReadPageSize = options.Value.SourceReadPageSize;
        _logger = logger ?? NullLogger<DefaultCompactor>.Instance;
    }

    /// <inheritdoc/>
    public async Task<CompactionResult> CompactAsync(
        CompactionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = request.Context;
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.ContextCompact);
        _ = activity?.SetTag(AgentKitTagNames.CompactionId, context.CompactionId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.AgentId, context.AgentId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.SessionId, context.SessionId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.OperationId, context.Correlation.OperationId.ToString());

        try
        {
            CompactionLog.Started(_logger, context.CompactionId, context.SessionId);
            var result = await CompactCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result switch
            {
                CompactionSucceeded => "succeeded",
                CompactionConflict => "conflict",
                CompactionCancelled => "cancelled",
                CompactionRejected => "rejected",
                CompactionNotReducing => "not_reducing",
                _ => "failed",
            };

            if (result is CompactionSucceeded)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, result.GetType().Name);
            }

            CompactionLog.Completed(_logger, context.CompactionId, context.SessionId, outcome);
            CompactionMetrics.Record(outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            CompactionLog.Cancelled(_logger, context.CompactionId, context.SessionId);
            CompactionMetrics.Record("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            CompactionLog.Failed(
                _logger,
                context.CompactionId,
                context.SessionId,
                exception.GetType().FullName ?? exception.GetType().Name);
            CompactionMetrics.Record("failed");
            throw;
        }
    }

    private async Task<CompactionResult> CompactCoreAsync(
        CompactionRequest request, CancellationToken cancellationToken)
    {

        var context = request.Context;
        var sessionContext = new SessionOperationContext(
            context.AgentId,
            context.SessionId,
            executionLaneId: null,
            context.Correlation,
            context.Identity,
            context.Authorization);

        var loadResult = await LoadSourceAsync(sessionContext, request, cancellationToken).ConfigureAwait(false);
        if (loadResult is not { } source)
        {
            return new CompactionFailed(
                context,
                new CompactionFailure(
                    CompactionFailureKind.SourceUnavailable,
                    "The eligible source range could not be loaded.",
                    retryable: true,
                    ExtensionData.Empty));
        }

        var cutResult = await _cutSelector.SelectAsync(
            new CompactionCutSelectionRequest(request, source), cancellationToken).ConfigureAwait(false);

        switch (cutResult)
        {
            case NoSafeCompactionCut noCut:
                return new CompactionRejected(context, noCut.Rejection);
            case CompactionCutSelectionFailed cutFailed:
                return new CompactionFailed(context, cutFailed.Failure);
            case CompactionCutSelected:
            default:
                break;
        }

        var cut = ((CompactionCutSelected) cutResult).Cut;

        var strategyResult = await _strategy.ProduceAsync(
            new CompactionStrategyRequest(request, source, cut), cancellationToken).ConfigureAwait(false);

        switch (strategyResult)
        {
            case CompactionStrategyUnsupported unsupported:
                return new CompactionRejected(context, unsupported.Rejection);
            case CompactionStrategyFailed strategyFailed:
                return new CompactionFailed(context, strategyFailed.Failure);
            case CompactionCheckpointProduced:
            default:
                break;
        }

        var produced = (CompactionCheckpointProduced) strategyResult;
        var coveredEntries = source.Entries.Where(e => cut.CoveredEntryIds.Contains(e.Id)).ToImmutableArray();
        var before = _estimator.EstimateEntries(coveredEntries);

        var manifest = new CompactionManifest(
            _manifestIds.Create(),
            context,
            request.BranchId,
            request.SourceVersion,
            cut.CoveredRange,
            cut.RetainedSuffixStart,
            produced.Producer,
            request.ContextEpoch,
            before,
            produced.After,
            _timeProvider.GetUtcNow(),
            ExtensionData.Empty);

        var candidate = new CompactionCandidate(manifest, produced.Checkpoint);

        var validationResult = await _validator.ValidateAsync(
            new CompactionValidationRequest(request, source, cut, candidate), cancellationToken)
            .ConfigureAwait(false);

        switch (validationResult)
        {
            case CompactionValidationFailed validationFailed:
                return new CompactionFailed(context, validationFailed.Failure);

            case CompactionValidationRejected rejected:
                if (rejected.Issues.Length == 1
                    && rejected.Issues[0].Kind == CompactionValidationIssueKind.NonReducing)
                {
                    return new CompactionNotReducing(context, before, produced.After, request.MinimumReductionRatio);
                }

                return new CompactionRejected(
                    context,
                    new CompactionRejection(
                        CompactionRejectionKind.PolicyViolation,
                        string.Join(' ', rejected.Issues.Select(static i => i.SafeMessage)),
                        ExtensionData.Empty));

            case CompactionValidated:
            default:
                break;
        }

        return await ActivateAsync(sessionContext, context, request, cut, manifest, candidate, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CompactionSourceSnapshot?> LoadSourceAsync(
        SessionOperationContext sessionContext, CompactionRequest request, CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = new SessionSequence(0);

        while (cursor.Value < request.SourceThrough.Value)
        {
            var pageResult = await _coordinator.ReadAsync(
                new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize),
                request.Context.SessionProfile,
                cancellationToken).ConfigureAwait(false);

            if (pageResult is not SessionPage page)
            {
                return null;
            }

            entries.AddRange(page.Entries);
            cursor = page.ThroughSequence;

            if (!page.HasMore || page.Entries.IsEmpty)
            {
                break;
            }
        }

        return new CompactionSourceSnapshot(request.Context, request.BranchId, request.SourceVersion, cursor, entries.ToImmutable());
    }

    private async Task<CompactionResult> ActivateAsync(
        SessionOperationContext sessionContext,
        CompactionOperationContext context,
        CompactionRequest request,
        CompactionCut cut,
        CompactionManifest manifest,
        CompactionCandidate candidate,
        CancellationToken cancellationToken)
    {
        var activatedVersion = new SessionVersion(request.SourceVersion.Value + 1);
        var lastCoveredId = cut.CoveredEntryIds[^1];

        var record = new CompactionRecord(
            context,
            request.SourceVersion,
            activatedVersion,
            CompactionRecordStatus.Active,
            manifest,
            candidate.Checkpoint,
            supersedes: null,
            rejection: null,
            _timeProvider.GetUtcNow(),
            ExtensionData.Empty);

        var entry = new CompactionSessionEntry(
            _entryIds.Create(),
            sessionContext.ToAddress(),
            context.Correlation,
            request.BranchId,
            new SessionSequence(activatedVersion.Value),
            lastCoveredId,
            _timeProvider.GetUtcNow(),
            new SchemaVersion("1.0"),
            record);

        var appendResult = await _coordinator.AppendAsync(
            new SessionAppendRequest(
                sessionContext,
                request.BranchId,
                request.SourceVersion,
                new IdempotencyKey($"compaction:{context.CompactionId}"),
                [entry]),
            request.Context.SessionProfile,
            cancellationToken).ConfigureAwait(false);

        return appendResult switch
        {
            SessionAppended => new CompactionSucceeded(context, record),
            SessionAppendConflict conflict => new CompactionConflict(
                context, conflict.ExpectedVersion, conflict.ActualVersion, manifest),
            SessionAppendNotFound => new CompactionFailed(
                context,
                new CompactionFailure(
                    CompactionFailureKind.ActivationFailure,
                    "The session or branch no longer exists.",
                    retryable: false,
                    ExtensionData.Empty)),
            SessionAppendFailed failed => new CompactionFailed(
                context,
                new CompactionFailure(
                    CompactionFailureKind.ActivationFailure, failed.SafeMessage, retryable: true, ExtensionData.Empty)),
            _ => new CompactionFailed(
                context,
                new CompactionFailure(
                    CompactionFailureKind.Unknown, "Unrecognized append outcome.", retryable: false, ExtensionData.Empty))
        };
    }
}
