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
/// <para>
/// This class composes exactly one <see cref="ICompactionCutSelector"/>,
/// one <see cref="ICompactionStrategy"/>, and one
/// <see cref="ICompactionValidator"/>; it never re-implements their
/// responsibilities inline.
/// </para>
/// <para>
/// Source loading pins every page to the first page's
/// <see cref="SessionReadSnapshot"/> and compares that snapshot's observed
/// version with <see cref="CompactionRequest.SourceVersion"/> before any
/// collaborator runs; a stale request is a <see cref="CompactionConflict"/>
/// without a manifest. It reads the whole pinned branch and then restricts
/// the snapshot handed to collaborators to entries whose sequence does not
/// exceed <see cref="CompactionRequest.SourceThrough"/>. The branch tip is kept
/// separately because it, not the eligible bound, decides the sequence of
/// the appended <see cref="CompactionSessionEntry"/>. A request whose
/// eligible bound lies beyond the tip fails with a non-retryable
/// <see cref="CompactionFailureKind.SourceUnavailable"/>, and a cut whose
/// covered entries are causal parents of ineligible later entries is
/// rejected with <see cref="CompactionRejectionKind.NoSafeCut"/> because the
/// selector cannot see that dependency.
/// </para>
/// <para>
/// Activation assumes that one committed append advances the session
/// version by exactly one regardless of how many entries it carries, and
/// that sequences advance by one per entry. Because the durable record must
/// carry its <see cref="CompactionRecord.ActivatedSessionVersion"/> before
/// the append is issued, the compactor precomputes it as
/// <c>SourceVersion + 1</c>, allocates the entry sequence as the observed
/// branch tip plus one, and then compares the store's reported
/// <see cref="SessionAppended.NewVersion"/> with the precomputed value. A
/// mismatch means the persisted record's version claim is false; it is
/// logged and surfaced as a non-retryable activation failure.
/// </para>
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
            CompactionResult result;
            try
            {
                result = await CompactCoreAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Activation converts its own cancellation into a reconciled outcome, so an exception reaching this
                // point means no activation append was ever issued.
                result = new CompactionCancelled(
                    context,
                    CompactionCommitState.NotAttempted,
                    "The compaction attempt was cancelled before activation was attempted.");
            }

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

            if (result is CompactionCancelled cancelled)
            {
                CompactionLog.Cancelled(_logger, context.CompactionId, context.SessionId, cancelled.CommitState);
            }
            else
            {
                CompactionLog.Completed(_logger, context.CompactionId, context.SessionId, outcome);
            }

            CompactionMetrics.Record(outcome);
            return result;
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

    /// <summary>
    /// Runs the attempt pipeline: deadline check, source load, cut selection, production, validation, activation.
    /// </summary>
    private async Task<CompactionResult> CompactCoreAsync(
        CompactionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public entry point validates the request.");

        var context = request.Context;
        if (_timeProvider.GetUtcNow() >= request.Deadline)
        {
            // Deadline is enforced before any protected read or append so an expired request has no side effect.
            return new CompactionRejected(
                context,
                new CompactionRejection(
                    CompactionRejectionKind.DeadlineExceeded,
                    "The request deadline had already passed when the attempt started.",
                    ExtensionData.Empty));
        }

        var sessionContext = new SessionOperationContext(
            context.AgentId,
            context.SessionId,
            executionLaneId: null,
            context.Correlation,
            context.Identity,
            context.Authorization);

        var (loaded, loadTerminal) = await LoadSourceAsync(sessionContext, request, cancellationToken).ConfigureAwait(false);
        if (loaded is null)
        {
            Debug.Assert(loadTerminal is not null, "A load that yields no source must yield a typed terminal result.");
            return loadTerminal;
        }

        var source = loaded.Snapshot;
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

        // The selector only sees eligible entries. An ineligible retained entry beyond SourceThrough may still depend on
        // a covered parent (a tool result whose call is the last eligible entry); such a cut is unsafe and fails closed.
        if (!loaded.IneligibleTailParents.IsEmpty && cut.CoveredEntryIds.Any(loaded.IneligibleTailParents.Contains))
        {
            return new CompactionRejected(
                context,
                new CompactionRejection(
                    CompactionRejectionKind.NoSafeCut,
                    "The selected cut would separate an entry beyond the eligible range from its covered causal parent.",
                    ExtensionData.Empty));
        }

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

        return await ActivateAsync(sessionContext, context, request, loaded.BranchTip, cut, manifest, candidate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the whole branch, splits it at <see cref="CompactionRequest.SourceThrough"/>, and returns either the
    /// eligible snapshot with the observed branch tip or the typed result that ends the attempt (a failure, a
    /// conflict, or the already-active record of an earlier attempt with the same <see cref="CompactionId"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every page after the first is pinned to the first page's <see cref="SessionReadSnapshot"/>, so a concurrent
    /// append cannot leak into a later page. The first page's observed version is compared with
    /// <see cref="CompactionRequest.SourceVersion"/> before any collaborator runs; a mismatch is a
    /// <see cref="CompactionConflict"/> without a manifest. A page without snapshot evidence fails closed as a
    /// non-retryable <see cref="CompactionFailureKind.SourceUnavailable"/>, and a continuation page carrying a
    /// different snapshot fails as a retryable one.
    /// </para>
    /// <para>
    /// The read always continues to the pinned tip rather than stopping at the eligible bound: the tip decides the
    /// sequence a newly appended entry must carry, and the ineligible tail decides whether a cut inside the eligible
    /// range would orphan a later causal dependent. A request whose eligible bound lies beyond the tip names a range
    /// this branch does not have and fails as a non-retryable <see cref="CompactionFailureKind.SourceUnavailable"/>.
    /// </para>
    /// </remarks>
    private async Task<(LoadedCompactionSource? Source, CompactionResult? Terminal)> LoadSourceAsync(
        SessionOperationContext sessionContext, CompactionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(sessionContext is not null, "The caller builds the session context before loading.");
        Debug.Assert(request is not null, "The caller validates the request before loading.");

        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = new SessionSequence(0);
        SessionReadSnapshot? pinned = null;

        while (true)
        {
            var pageResult = await _coordinator.ReadAsync(
                pinned is null
                    ? new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize)
                    : new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize, pinned),
                request.Context.SessionProfile,
                cancellationToken).ConfigureAwait(false);

            if (pageResult is not SessionPage page)
            {
                return (null, new CompactionFailed(
                    request.Context,
                    new CompactionFailure(
                        CompactionFailureKind.SourceUnavailable,
                        "The eligible source range could not be loaded.",
                        retryable: true,
                        ExtensionData.Empty)));
            }

            if (page.Snapshot is not { } pageSnapshot)
            {
                // Without exact snapshot evidence the observed version and tip cannot be established; fail closed.
                return (null, new CompactionFailed(
                    request.Context,
                    new CompactionFailure(
                        CompactionFailureKind.SourceUnavailable,
                        "The session store did not supply exact read snapshot evidence.",
                        retryable: false,
                        ExtensionData.Empty)));
            }

            if (pinned is null)
            {
                if (pageSnapshot.Version != request.SourceVersion)
                {
                    // A branch that advanced past the request may have done so through an earlier attempt of this
                    // very checkpoint whose response was lost; an identical CompactionId then means "already active".
                    if (pageSnapshot.Version.Value > request.SourceVersion.Value)
                    {
                        var (existing, _) = await FindCommittedRecordAsync(
                            sessionContext, request, request.SourceThrough, pageSnapshot, maximumPages: int.MaxValue, cancellationToken)
                            .ConfigureAwait(false);
                        if (existing is not null)
                        {
                            return (null, new CompactionSucceeded(request.Context, existing));
                        }
                    }

                    return (null, new CompactionConflict(request.Context, request.SourceVersion, pageSnapshot.Version));
                }

                pinned = pageSnapshot;
            }
            else if (pageSnapshot != pinned)
            {
                return (null, new CompactionFailed(
                    request.Context,
                    new CompactionFailure(
                        CompactionFailureKind.SourceUnavailable,
                        "The branch changed while the source was being read.",
                        retryable: true,
                        ExtensionData.Empty)));
            }

            entries.AddRange(page.Entries);
            cursor = page.ThroughSequence;

            if (!page.HasMore || page.Entries.IsEmpty)
            {
                break;
            }
        }

        var branchTip = pinned.UpperSequence;
        if (request.SourceThrough.Value > branchTip.Value)
        {
            return (null, new CompactionFailed(
                request.Context,
                new CompactionFailure(
                    CompactionFailureKind.SourceUnavailable,
                    "The requested eligible range extends beyond the branch tip.",
                    retryable: false,
                    ExtensionData.Empty)));
        }

        var eligible = ImmutableArray.CreateBuilder<SessionEntry>(entries.Count);
        var tailParents = ImmutableHashSet.CreateBuilder<SessionEntryId>();
        foreach (var entry in entries)
        {
            if (entry.Sequence.Value <= request.SourceThrough.Value)
            {
                eligible.Add(entry);
            }
            else if (entry.CausalParentId is { } parentId)
            {
                _ = tailParents.Add(parentId);
            }
        }

        var snapshot = new CompactionSourceSnapshot(
            request.Context, request.BranchId, request.SourceVersion, request.SourceThrough, eligible.ToImmutable());

        return (new LoadedCompactionSource(snapshot, branchTip, tailParents.ToImmutable()), null);
    }

    private async Task<CompactionResult> ActivateAsync(
        SessionOperationContext sessionContext,
        CompactionOperationContext context,
        CompactionRequest request,
        SessionSequence branchTip,
        CompactionCut cut,
        CompactionManifest manifest,
        CompactionCandidate candidate,
        CancellationToken cancellationToken)
    {
        // Version and sequence advance independently (one version per append, one sequence per entry), so the
        // new entry's sequence follows the branch tip actually read, never "version + 1" or "SourceThrough + 1".
        // The activated version is precomputed as SourceVersion + 1 (one version per append) because the record must
        // carry it before the append; the store's reported NewVersion is reconciled against it afterwards.
        var activatedVersion = new SessionVersion(request.SourceVersion.Value + 1);
        var nextSequence = new SessionSequence(branchTip.Value + 1);
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
            nextSequence,
            lastCoveredId,
            _timeProvider.GetUtcNow(),
            new SchemaVersion("1"),
            record);

        var appendRequest = new SessionAppendRequest(
            sessionContext,
            request.BranchId,
            request.SourceVersion,
            new IdempotencyKey($"compaction:{context.CompactionId}"),
            [entry]);

        try
        {
            var appendResult = await _coordinator.AppendAsync(appendRequest, request.Context.SessionProfile, cancellationToken)
                .ConfigureAwait(false);

            return appendResult switch
            {
                SessionAppended appended => ReconcileActivatedVersion(context, record, activatedVersion, appended),
                SessionAppendConflict conflict => await ReconcileConflictAsync(
                    sessionContext, request, branchTip, manifest, conflict, cancellationToken).ConfigureAwait(false),
                SessionAppendNotFound => new CompactionFailed(
                    context,
                    new CompactionFailure(
                        CompactionFailureKind.ActivationFailure,
                        "The session or branch no longer exists.",
                        retryable: false,
                        ExtensionData.Empty)),
                SessionAppendFailed failed => await ReconcileFailedAppendAsync(
                    sessionContext, request, branchTip, failed, cancellationToken).ConfigureAwait(false),
                _ => new CompactionFailed(
                    context,
                    new CompactionFailure(
                        CompactionFailureKind.Unknown, "Unrecognized append outcome.", retryable: false, ExtensionData.Empty))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The append was issued; the caller's token no longer governs whether it committed. Reconcile without it.
            return await ReconcileCancelledActivationAsync(sessionContext, request, branchTip).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Establishes the truthful commit state after cancellation was observed during or after the activation append,
    /// using a bounded reconciliation read that is deliberately not governed by the caller's cancellation token.
    /// </summary>
    private async Task<CompactionResult> ReconcileCancelledActivationAsync(
        SessionOperationContext sessionContext, CompactionRequest request, SessionSequence branchTip)
    {
        Debug.Assert(sessionContext is not null, "The caller builds the session context before activating.");
        Debug.Assert(request is not null, "The caller validates the request before activating.");

        var (existing, reconciled) = await FindCommittedRecordAsync(
            sessionContext, request, branchTip, pinned: null, maximumPages: 1, CancellationToken.None).ConfigureAwait(false);

        return existing is not null
            ? new CompactionCancelled(
                request.Context,
                CompactionCommitState.Committed,
                "The compaction attempt was cancelled after its record was committed.",
                existing)
            : reconciled
            ? new CompactionCancelled(
                request.Context,
                CompactionCommitState.NotCommitted,
                "The compaction attempt was cancelled during activation; no record was committed.")
            : new CompactionCancelled(
                request.Context,
                CompactionCommitState.Unknown,
                "The compaction attempt was cancelled during activation and its commit state could not be reconciled.");
    }

    /// <summary>
    /// Confirms that the store's reported version equals the version the persisted record claims. The record is
    /// built before the append under the one-version-per-append assumption; a store that reports anything else has
    /// committed a record whose <see cref="CompactionRecord.ActivatedSessionVersion"/> is false, which is logged and
    /// surfaced as a non-retryable <see cref="CompactionFailureKind.ActivationFailure"/> rather than a clean success.
    /// </summary>
    private CompactionResult ReconcileActivatedVersion(
        CompactionOperationContext context,
        CompactionRecord record,
        SessionVersion activatedVersion,
        SessionAppended appended)
    {
        Debug.Assert(context is not null && record is not null && appended is not null, "The caller supplies the append outcome.");

        if (appended.NewVersion == activatedVersion)
        {
            return new CompactionSucceeded(context, record);
        }

        CompactionLog.ActivatedVersionMismatch(_logger, context.CompactionId, context.SessionId, activatedVersion, appended.NewVersion);
        return new CompactionFailed(
            context,
            new CompactionFailure(
                CompactionFailureKind.ActivationFailure,
                $"The record was committed claiming activated version {activatedVersion.Value} but the store reported version {appended.NewVersion.Value}; the persisted version claim is false.",
                retryable: false,
                ExtensionData.Empty));
    }

    /// <summary>
    /// Resolves an append conflict: a duplicate attempt of the same checkpoint may have won the version check, in
    /// which case the checkpoint is already active and the conflict is not reported.
    /// </summary>
    private async Task<CompactionResult> ReconcileConflictAsync(
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence branchTip,
        CompactionManifest manifest,
        SessionAppendConflict conflict,
        CancellationToken cancellationToken)
    {
        Debug.Assert(conflict is not null, "The caller matched an append conflict.");
        Debug.Assert(manifest is not null, "A candidate exists once activation is attempted.");

        var (existing, _) = await FindCommittedRecordAsync(
            sessionContext, request, branchTip, pinned: null, maximumPages: 1, cancellationToken).ConfigureAwait(false);
        return existing is not null
            ? new CompactionSucceeded(request.Context, existing)
            : new CompactionConflict(request.Context, conflict.ExpectedVersion, conflict.ActualVersion, manifest);
    }

    /// <summary>
    /// Resolves a failed append. The store does not distinguish a deterministic rejection (a reused idempotency key
    /// with different evidence, a sequence mismatch) from a lost response after commit, so the branch is reconciled by
    /// <see cref="CompactionId"/> first; when no record exists the failure is reported as non-retryable because a
    /// blind repeat cannot present identical idempotency evidence and must not drive an unbounded retry loop.
    /// </summary>
    private async Task<CompactionResult> ReconcileFailedAppendAsync(
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence branchTip,
        SessionAppendFailed failed,
        CancellationToken cancellationToken)
    {
        Debug.Assert(failed is not null, "The caller matched a failed append.");

        var (existing, reconciled) = await FindCommittedRecordAsync(
            sessionContext, request, branchTip, pinned: null, maximumPages: 1, cancellationToken).ConfigureAwait(false);
        return existing is not null
            ? new CompactionSucceeded(request.Context, existing)
            : new CompactionFailed(
            request.Context,
            new CompactionFailure(
                CompactionFailureKind.ActivationFailure,
                reconciled
                    ? $"{failed.SafeMessage} No record for this compaction was committed."
                    : $"{failed.SafeMessage} The commit state could not be reconciled.",
                retryable: false,
                ExtensionData.Empty));
    }

    /// <summary>
    /// Scans the branch after <paramref name="fromSequenceExclusive"/> for an active
    /// <see cref="CompactionSessionEntry"/> whose record carries the request's <see cref="CompactionId"/>.
    /// </summary>
    /// <remarks>
    /// This is the reconciliation path that makes <see cref="CompactionId"/> idempotent: a retried or racing attempt
    /// of the same logical checkpoint discovers the committed record instead of failing on reused idempotency
    /// evidence. Reads continue from <paramref name="pinned"/> when supplied and otherwise pin to the first page
    /// returned; a read failure or a drifted continuation snapshot reports <c>Reconciled = false</c> so the caller
    /// can distinguish "no record exists" from "commit state unknown". Because an activation append is
    /// version-checked, a record committed by this attempt is necessarily the first branch entry after the tip it
    /// observed, so callers scanning from that tip bound the read to one page; the load-time scan after
    /// <see cref="CompactionRequest.SourceThrough"/> must cross the ineligible tail and is bounded by the branch.
    /// </remarks>
    /// <returns>
    /// The committed record and <see langword="true"/> when found; <see langword="null"/> and
    /// <see langword="true"/> when the scan completed without a match; <see langword="null"/> and
    /// <see langword="false"/> when the scan could not be completed.
    /// </returns>
    private async Task<(CompactionRecord? Record, bool Reconciled)> FindCommittedRecordAsync(
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence fromSequenceExclusive,
        SessionReadSnapshot? pinned,
        int maximumPages,
        CancellationToken cancellationToken)
    {
        Debug.Assert(sessionContext is not null, "The caller builds the session context before reconciling.");
        Debug.Assert(request is not null, "The caller validates the request before reconciling.");
        Debug.Assert(maximumPages > 0, "Reconciliation reads at least one page.");

        var cursor = fromSequenceExclusive;
        for (var pagesRead = 0; pagesRead < maximumPages; pagesRead++)
        {
            var pageResult = await _coordinator.ReadAsync(
                pinned is null
                    ? new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize)
                    : new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize, pinned),
                request.Context.SessionProfile,
                cancellationToken).ConfigureAwait(false);

            if (pageResult is not SessionPage page)
            {
                return (null, false);
            }

            if (pinned is null)
            {
                pinned = page.Snapshot;
            }
            else if (page.Snapshot != pinned)
            {
                return (null, false);
            }

            foreach (var entry in page.Entries)
            {
                if (entry is CompactionSessionEntry { Record: { Status: CompactionRecordStatus.Active } committed }
                    && committed.Context.CompactionId == request.Context.CompactionId)
                {
                    return (committed, true);
                }
            }

            cursor = page.ThroughSequence;
            if (!page.HasMore || page.Entries.IsEmpty)
            {
                return (null, true);
            }
        }

        // The page bound was reached; a record committed by this attempt would have been on the first page.
        return (null, true);
    }
}
