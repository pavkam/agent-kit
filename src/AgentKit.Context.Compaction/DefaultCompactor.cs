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
    private readonly ICompactionStrategyResolver _strategies;
    private readonly ICompactionValidator _validator;
    private readonly ICompactionActivationCoordinator _activation;
    private readonly ICompactionEventDispatcher _events;
    private readonly ICompactionSizeEstimator _estimator;
    private readonly IIdentifierGenerator<CompactionManifestId> _manifestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ComponentKey<ICompactor> _compactorKey;
    private readonly int _sourceReadPageSize;
    private readonly ILogger<DefaultCompactor> _logger;

    /// <summary>Initializes a new instance of the <see cref="DefaultCompactor"/> class.</summary>
    public DefaultCompactor(
        ISessionCoordinator coordinator,
        ICompactionCutSelector cutSelector,
        ICompactionStrategyResolver strategies,
        ICompactionValidator validator,
        ICompactionActivationCoordinator activation,
        ICompactionEventDispatcher events,
        ICompactionSizeEstimator estimator,
        IIdentifierGenerator<CompactionManifestId> manifestIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider,
        IOptions<CompactionOptions> options,
        ContextCompactionOptionsSnapshot compactionOptions,
        ILogger<DefaultCompactor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(cutSelector);
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(activation);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(manifestIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(compactionOptions);

        _coordinator = coordinator;
        _cutSelector = cutSelector;
        _strategies = strategies;
        _validator = validator;
        _activation = activation;
        _events = events;
        _estimator = estimator;
        _manifestIds = manifestIds;
        _timeProvider = timeProvider;
        _compactorKey = compactionOptions.CompactorKey;
        _sourceReadPageSize = options.Value.SourceReadPageSize;
        _logger = logger ?? NullLogger<DefaultCompactor>.Instance;
        _ = entryIds;
    }

    /// <summary>Initializes a compactor for tests and legacy call sites with one fixed strategy.</summary>
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
        : this(
            coordinator,
            cutSelector,
            new FixedCompactionStrategyResolver(strategy),
            validator,
            new SessionCompactionActivationCoordinator(
                entryIds,
                timeProvider,
                options ?? throw new ArgumentNullException(nameof(options))),
            new DefaultCompactionEventDispatcher(
                AgentContextCompactionComponentDefaults.CompactorKey,
                [],
                EmptyServiceProvider.Instance),
            estimator,
            manifestIds,
            entryIds,
            timeProvider,
            options,
            new ContextCompactionOptionsSnapshot(
                AgentContextCompactionComponentDefaults.CompactorKey,
                maximumAttempts: 2,
                maximumSourceEntries: options.Value.MaximumSourceEntries,
                maximumSourceBytes: 8 * 1024 * 1024,
                maximumSummaryTokens: 2048,
                minimumRetainedEntries: 8,
                maximumValidationIssues: 64,
                minimumReductionRatio: 0.20,
                attemptTimeout: TimeSpan.FromMinutes(2),
                persistRejectedCandidates: false),
            logger) => ArgumentNullException.ThrowIfNull(options);

    /// <inheritdoc/>
    public Task<CompactionResult> CompactAsync(
        CompactionRequest request,
        SessionExecutionCapability session,
        BudgetExecutionCapability budget,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(budget);
        return session.Profile.Reference.Key != request.Context.SessionProfile.Reference.Key
            ? throw new ArgumentException("The session capability does not match the compaction request profile.")
            : CompactCoreWithObservationAsync(request, session, budget, hooks, cancellationToken);
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
                result = await CompactCoreAsync(request, session: null, budget: null, hooks: null, cancellationToken)
                    .ConfigureAwait(false);
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

    private async Task<CompactionResult> CompactCoreWithObservationAsync(
        CompactionRequest request,
        SessionExecutionCapability session,
        BudgetExecutionCapability budget,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken)
    {
        var started = await _events.PublishAsync(
            _compactorKey,
            new CompactionAttemptStartedEvent(request.Context, _timeProvider.GetUtcNow(), request.Trigger),
            cancellationToken).ConfigureAwait(false);
        if (started is RequiredCompactionEventUnavailable unavailable)
        {
            return new CompactionFailed(
                request.Context,
                unavailable.Failure);
        }

        var result = await CompactCoreAsync(request, session, budget, hooks, cancellationToken).ConfigureAwait(false);
        _ = await _events.PublishAsync(
            _compactorKey,
            new CompactionAttemptFinishedEvent(request.Context, _timeProvider.GetUtcNow(), ToOutcomeSummary(result)),
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Runs the attempt pipeline: deadline check, source load, cut selection, production, validation, activation.
    /// </summary>
    private async Task<CompactionResult> CompactCoreAsync(
        CompactionRequest request,
        SessionExecutionCapability? session,
        BudgetExecutionCapability? budget,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public entry point validates the request.");
        _ = hooks;

        var context = request.Context;
        var coordinator = session?.Coordinator ?? _coordinator;
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

        var (loaded, loadTerminal) = await LoadSourceAsync(sessionContext, request, coordinator, cancellationToken)
            .ConfigureAwait(false);
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

        var strategyKey = request.Policy?.StrategyOrder is { Length: > 0 } order
            ? order[0]
            : ExtractiveCompactionStrategy.StrategyKey;
        var strategyResolution = await _strategies
            .ResolveAsync(_compactorKey, strategyKey, cancellationToken)
            .ConfigureAwait(false);
        if (strategyResolution is CompactionStrategyNotFound notFound)
        {
            return new CompactionFailed(
                context,
                new CompactionFailure(
                    CompactionFailureKind.StrategyFailure,
                    $"No compaction strategy '{notFound.StrategyKey}' is registered for compactor '{notFound.CompactorKey}'.",
                    retryable: false,
                    ExtensionData.Empty));
        }

        var strategy = ((CompactionStrategyResolved) strategyResolution).Strategy;
        var strategyResult = await strategy.ProduceAsync(
            new CompactionStrategyRequest(request, source, cut),
            budget,
            cancellationToken).ConfigureAwait(false);

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
        var coveredIds = cut.CoveredEntryIds.ToImmutableHashSet();
        var coveredEntries = source.Entries.Where(e => coveredIds.Contains(e.Id)).ToImmutableArray();
        var before = _estimator.EstimateEntries(coveredEntries);

        // The strategy's self-reported size is advisory; the durable manifest and the not-reducing outcome carry the
        // compactor's own estimate, computed with the same estimator the validator uses.
        var after = _estimator.EstimateCheckpoint(produced.Checkpoint);

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
            after,
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
                    return new CompactionNotReducing(context, before, after, request.MinimumReductionRatio);
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

        // A newer active record names the prior CompactionId in Supersedes rather than mutating the
        // older record (docs/architecture/context-compaction.md); scan the covered range for the
        // newest earlier active record this cut subsumes. coveredEntries is in branch order, so the
        // last match is the newest.
        var supersedes = coveredEntries
            .OfType<CompactionSessionEntry>()
            .LastOrDefault(static entry => entry.Record.Status == CompactionRecordStatus.Active)
            ?.Record.Context.CompactionId;

        var validated = (CompactionValidated) validationResult;
        var validatedEvent = await _events.PublishAsync(
            _compactorKey,
            new CompactionCandidateValidatedEvent(context, _timeProvider.GetUtcNow(), manifest),
            cancellationToken).ConfigureAwait(false);
        if (validatedEvent is RequiredCompactionEventUnavailable requiredUnavailable)
        {
            return new CompactionFailed(context, requiredUnavailable.Failure);
        }

        var sessionCapability = session
            ?? new SessionExecutionCapability(context.SessionProfile, coordinator, InertSessionRunCoordinator.Instance);
        var activationRequest = new CompactionActivationRequest(
            request,
            validated.Compaction,
            request.SourceVersion,
            loaded.BranchTip,
            cut.CoveredEntryIds[^1],
            new IdempotencyKey($"compaction:{context.CompactionId}"),
            supersedes);
        var activationResult = await _activation.ActivateAsync(
            activationRequest,
            sessionCapability,
            CompactionActivationGrantFactory.Create(context, _timeProvider),
            cancellationToken).ConfigureAwait(false);
        return MapActivationResult(activationResult, context, manifest);
    }

    private CompactionResult MapActivationResult(
        CompactionActivationResult activation,
        CompactionOperationContext context,
        CompactionManifest manifest) =>
        activation switch
        {
            CompactionRecordActivated activated => new CompactionSucceeded(context, activated.Record),
            CompactionRecordConflict conflict => new CompactionConflict(
                context, conflict.ExpectedVersion, conflict.ActualVersion, manifest),
            CompactionRecordActivationRejected rejected => new CompactionRejected(context, rejected.Rejection),
            CompactionRecordActivationFailed failed => MapActivationFailed(context, failed),
            CompactionRecordActivationCancelled cancelled => new CompactionCancelled(
                context,
                cancelled.Cancellation.CommitState,
                cancelled.Cancellation.SafeMessage,
                cancelled.Cancellation.CommittedRecord),
            _ => new CompactionFailed(
                context,
                new CompactionFailure(
                    CompactionFailureKind.Unknown,
                    "Unrecognized activation outcome.",
                    retryable: false,
                    ExtensionData.Empty))
        };

    private CompactionFailed MapActivationFailed(
        CompactionOperationContext context,
        CompactionRecordActivationFailed failed)
    {
        if (failed.ActivatedVersionClaim is { } expected && failed.StoreReportedVersion is { } reported)
        {
            CompactionLog.ActivatedVersionMismatch(
                _logger,
                context.CompactionId,
                context.SessionId,
                expected,
                reported);
        }

        return new CompactionFailed(context, failed.Failure);
    }

    private static CompactionOutcomeSummary ToOutcomeSummary(CompactionResult result) =>
        result switch
        {
            CompactionSucceeded succeeded => new(
                CompactionOutcomeKind.Succeeded,
                succeeded.Record.Manifest.Id,
                null,
                succeeded.Record.ActivatedSessionVersion,
                null),
            CompactionConflict => new(CompactionOutcomeKind.Conflict, null, null, null, null),
            CompactionCancelled => new(CompactionOutcomeKind.Cancelled, null, null, null, null),
            CompactionRejected => new(CompactionOutcomeKind.Rejected, null, null, null, null),
            CompactionNotReducing => new(CompactionOutcomeKind.NotReducing, null, null, null, null),
            CompactionFailed failed => new(CompactionOutcomeKind.Failed, null, null, null, failed.Failure.SafeMessage),
            _ => new(CompactionOutcomeKind.Failed, null, null, null, "Unknown compaction outcome.")
        };

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
        SessionOperationContext sessionContext,
        CompactionRequest request,
        ISessionCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        Debug.Assert(sessionContext is not null, "The caller builds the session context before loading.");
        Debug.Assert(request is not null, "The caller validates the request before loading.");

        var entries = ImmutableArray.CreateBuilder<SessionEntry>();
        var cursor = new SessionSequence(0);
        SessionReadSnapshot? pinned = null;

        while (true)
        {
            var pageResult = await coordinator.ReadAsync(
                pinned is null
                    ? new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize)
                    : new SessionReadRequest(sessionContext, request.BranchId, cursor, _sourceReadPageSize, pinned),
                request.Context.SessionProfile,
                cancellationToken).ConfigureAwait(false);

            if (pageResult is not SessionPage page)
            {
                // Not-found is deterministic for this identity and address; only a store/transport failure is transient.
                return (null, new CompactionFailed(
                    request.Context,
                    new CompactionFailure(
                        CompactionFailureKind.SourceUnavailable,
                        pageResult is SessionReadNotFound
                            ? "The session or branch does not exist or is not visible to this identity."
                            : "The eligible source range could not be loaded.",
                        retryable: pageResult is not SessionReadNotFound,
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
                            coordinator,
                            sessionContext,
                            request,
                            request.SourceThrough,
                            pageSnapshot,
                            maximumPages: int.MaxValue,
                            cancellationToken).ConfigureAwait(false);
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
        ISessionCoordinator coordinator,
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence fromSequenceExclusive,
        SessionReadSnapshot? pinned,
        int maximumPages,
        CancellationToken cancellationToken)
    {
        Debug.Assert(coordinator is not null, "The caller supplies the session coordinator.");
        Debug.Assert(sessionContext is not null, "The caller builds the session context before reconciling.");
        Debug.Assert(request is not null, "The caller validates the request before reconciling.");
        Debug.Assert(maximumPages > 0, "Reconciliation reads at least one page.");

        var cursor = fromSequenceExclusive;
        for (var pagesRead = 0; pagesRead < maximumPages; pagesRead++)
        {
            var pageResult = await coordinator.ReadAsync(
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
