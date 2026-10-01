// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>Session-backed compaction activation coordinator.</summary>
/// <remarks>
/// <para>
/// Activation is the one compaction boundary whose interruption is ambiguous: cut selection, summary generation, and
/// validation compute a candidate without changing durable session truth, while the append either committed or did
/// not. When the request carries a <see cref="CompactionRequest.DurabilityProfile"/> and that profile enables
/// <see cref="CompactionDurableOperations.Activation"/>, the append runs inside a recoverable operation so a lost
/// process leaves evidence of the attempt. Every other composition runs the identical append path.
/// </para>
/// <para>
/// Instances are engine-wide singletons and safe for concurrent use.
/// </para>
/// </remarks>
public sealed class SessionCompactionActivationCoordinator: ICompactionActivationCoordinator
{
    /// <summary>The window added to the injected clock for one activation operation's declared deadline.</summary>
    private static readonly TimeSpan _durableOperationTimeout = TimeSpan.FromMinutes(10);

    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly TimeProvider _timeProvider;
    private readonly int _sourceReadPageSize;
    private readonly IDurableExecutionCoordinator? _durableExecution;
    private readonly IDurabilityProfileCatalog? _durabilityProfiles;
    private readonly DurableBoundaryRegistry _durableInvocations;

    /// <summary>Initializes the session-backed activation coordinator.</summary>
    /// <param name="entryIds">The non-null generator that mints the compaction entry's session identity.</param>
    /// <param name="timeProvider">The non-null injected clock used for record instants and operation deadlines.</param>
    /// <param name="options">The non-null validated compaction configuration supplying the source read page size.</param>
    /// <param name="durableExecution">
    /// The composed durable execution coordinator, or <see langword="null"/> when durability is not composed. It is
    /// used only to journal the activation append and never to decide whether a candidate may activate.
    /// </param>
    /// <param name="durabilityProfiles">
    /// The composed durability profile catalog, or <see langword="null"/> when durability is not composed.
    /// </param>
    /// <param name="durableInvocations">
    /// The engine-wide live boundary continuation registry, or <see langword="null"/> to use a private instance.
    /// Supplying the shared singleton is what lets the coordinator reach this component's live append.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entryIds"/>, <paramref name="timeProvider"/>, or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    public SessionCompactionActivationCoordinator(
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider timeProvider,
        IOptions<CompactionOptions> options,
        IDurableExecutionCoordinator? durableExecution = null,
        IDurabilityProfileCatalog? durabilityProfiles = null,
        DurableBoundaryRegistry? durableInvocations = null)
    {
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _entryIds = entryIds;
        _timeProvider = timeProvider;
        _sourceReadPageSize = options.Value.SourceReadPageSize;
        _durableExecution = durableExecution;
        _durabilityProfiles = durabilityProfiles;
        _durableInvocations = durableInvocations ?? new DurableBoundaryRegistry();
    }

    /// <inheritdoc/>
    public async Task<CompactionActivationResult> ActivateAsync(
        CompactionActivationRequest request,
        SessionExecutionCapability session,
        SecurityGrant activationGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(activationGrant);
        _ = activationGrant;

        if (session.Profile.Reference.Key != request.Request.Context.SessionProfile.Reference.Key)
        {
            return new CompactionRecordActivationRejected(
                new CompactionRejection(
                    CompactionRejectionKind.PolicyViolation,
                    "The session capability does not match the compaction request profile.",
                    ExtensionData.Empty));
        }

        var compactionRequest = request.Request;
        var context = compactionRequest.Context;
        var candidate = request.Compaction.Candidate;
        var manifest = candidate.Manifest;
        var coordinator = session.Coordinator;

        var sessionContext = new SessionOperationContext(
            context.AgentId,
            context.SessionId,
            executionLaneId: null,
            context.Correlation,
            context.Identity,
            context.Authorization);

        var activatedVersion = new SessionVersion(compactionRequest.SourceVersion.Value + 1);
        var nextSequence = new SessionSequence(request.BranchTip.Value + 1);

        var record = new CompactionRecord(
            context,
            compactionRequest.SourceVersion,
            activatedVersion,
            CompactionRecordStatus.Active,
            manifest,
            candidate.Checkpoint,
            request.Supersedes,
            rejection: null,
            _timeProvider.GetUtcNow(),
            ExtensionData.Empty);

        var entry = new CompactionSessionEntry(
            _entryIds.Create(),
            sessionContext.ToAddress(),
            context.Correlation,
            compactionRequest.BranchId,
            nextSequence,
            request.LastCoveredEntryId,
            _timeProvider.GetUtcNow(),
            new SchemaVersion("1"),
            record);

        var appendRequest = new SessionAppendRequest(
            sessionContext,
            compactionRequest.BranchId,
            compactionRequest.SourceVersion,
            request.IdempotencyKey,
            [entry]);

        try
        {
            var appendResult = await AppendActivationAsync(
                coordinator, compactionRequest, appendRequest, activatedVersion, request, cancellationToken)
                .ConfigureAwait(false);

            return appendResult switch
            {
                SessionAppended appended when appended.NewVersion == activatedVersion =>
                    new CompactionRecordActivated(record),
                SessionAppended appended => new CompactionRecordActivationFailed(
                    new CompactionFailure(
                        CompactionFailureKind.ActivationFailure,
                        $"The record was committed claiming activated version {activatedVersion.Value} but the store reported version {appended.NewVersion.Value}.",
                        retryable: false,
                        ExtensionData.Empty),
                    activatedVersion,
                    appended.NewVersion),
                SessionAppendConflict conflict => await ReconcileConflictAsync(
                    coordinator, sessionContext, compactionRequest, request.BranchTip, manifest, conflict, cancellationToken)
                    .ConfigureAwait(false),
                SessionAppendNotFound => new CompactionRecordActivationFailed(
                    new CompactionFailure(
                        CompactionFailureKind.ActivationFailure,
                        "The session or branch no longer exists.",
                        retryable: false,
                        ExtensionData.Empty)),
                SessionAppendFailed failed => await ReconcileFailedAppendAsync(
                    coordinator, sessionContext, compactionRequest, request.BranchTip, failed, cancellationToken)
                    .ConfigureAwait(false),
                _ => new CompactionRecordActivationFailed(
                    new CompactionFailure(
                        CompactionFailureKind.Unknown,
                        "Unrecognized append outcome.",
                        retryable: false,
                        ExtensionData.Empty))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var (existing, reconciled) = await FindCommittedRecordAsync(
                coordinator, sessionContext, compactionRequest, request.BranchTip, null, 1, CancellationToken.None)
                .ConfigureAwait(false);
            return existing is not null
                ? new CompactionRecordActivationCancelled(
                    new CompactionCancellation(
                        CompactionCancellationReason.CallerCancelled,
                        CompactionCommitState.Committed,
                        "The compaction attempt was cancelled during activation after the record was committed.",
                        existing))
                : new CompactionRecordActivationCancelled(
                    new CompactionCancellation(
                        CompactionCancellationReason.CallerCancelled,
                        reconciled ? CompactionCommitState.NotCommitted : CompactionCommitState.Unknown,
                        reconciled
                            ? "The compaction attempt was cancelled during activation; no record was committed."
                            : "The compaction attempt was cancelled during activation and its commit state could not be reconciled."));
        }
    }

    /// <summary>Appends the activation entry, journaling it first when the request's profile enables that boundary.</summary>
    /// <param name="coordinator">The session coordinator that performs the append either way.</param>
    /// <param name="compactionRequest">The compaction request whose profile selection gates journaling.</param>
    /// <param name="appendRequest">The exact idempotent append this activation performs.</param>
    /// <param name="activatedVersion">The branch version the activated record claims.</param>
    /// <param name="request">The activation attempt, supplying the covered-range evidence recorded in the manifest.</param>
    /// <param name="cancellationToken">Cancels the append and any local awaiting of the durable operation.</param>
    /// <returns>Exactly the session coordinator's own append result.</returns>
    /// <remarks>
    /// <para>
    /// The boundary is journaled under the compaction operation's own captured authorization, because a durable
    /// write is a protected effect addressed to this operation. A capture that cannot be durably addressed — a
    /// sessionless or before-run scope — leaves the append unjournaled rather than being given an invented run
    /// identity; compaction legitimately runs outside a turn, but never outside a session.
    /// </para>
    /// <para>
    /// The checkpoint is committed before the append rather than after it. That is the boundary recovery needs:
    /// after the checkpoint the record may or may not be in the session, which is exactly the question the existing
    /// reconciliation read answers, whereas a checkpoint written only on success would leave a crash mid-append
    /// indistinguishable from an attempt that never started.
    /// </para>
    /// </remarks>
    private async Task<SessionAppendResult> AppendActivationAsync(
        ISessionCoordinator coordinator,
        CompactionRequest compactionRequest,
        SessionAppendRequest appendRequest,
        SessionVersion activatedVersion,
        CompactionActivationRequest request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(coordinator is not null, "The session capability supplies the coordinator.");
        Debug.Assert(appendRequest is not null, "A complete append request is built before it is journaled.");
        if (DurableBoundaryScope.TryCreate(
                compactionRequest.DurabilityProfile,
                _durableExecution,
                _durabilityProfiles,
                _durableInvocations,
                _timeProvider,
                _durableOperationTimeout,
                out var durability) is not null
            || durability is null
            || !durability.Journals(CompactionDurableOperations.Activation, compactionRequest.Context.Authorization))
        {
            return await coordinator
                .AppendAsync(appendRequest, compactionRequest.Context.SessionProfile, cancellationToken)
                .ConfigureAwait(false);
        }

        var manifest = new DurableCompactionActivationManifest(
            compactionRequest.Context.CompactionId.Value,
            compactionRequest.SourceVersion.Value,
            activatedVersion.Value,
            CoveredEntryCount(request.Compaction.Candidate.Manifest.CoveredRange));
        return await durability.ExecuteAsync(
            CompactionDurableOperations.Activation,
            CompactionDurableOperations.ActivationVersion,
            compactionRequest.Context.Authorization,
            DurableBoundaryPayload.Encode(manifest),
            SecurityEffect.Append,
            hooks: null,
            async (context, token) =>
            {
                (await context.Checkpoints.RecordCheckpointAsync(
                    DurableCheckpointKind.CompactionActivated, context.Operation.Input, token).ConfigureAwait(false))
                    .ThrowIfNotRecorded();
                return await coordinator
                    .AppendAsync(appendRequest, compactionRequest.Context.SessionProfile, token)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts how many source sequences one inclusive covered range spans.</summary>
    /// <param name="range">The candidate's inclusive covered range.</param>
    /// <returns>The nonnegative span, saturated at <see cref="int.MaxValue"/> for a range wider than one manifest can count.</returns>
    /// <remarks>
    /// The count is derived from the range rather than a materialized entry list because the manifest records
    /// coverage as sequences. A saturating conversion keeps the durable record writable for an implausibly wide
    /// range instead of failing an activation over a diagnostic count.
    /// </remarks>
    private static int CoveredEntryCount(CompactionSourceRange range)
    {
        Debug.Assert(range is not null, "A validated candidate always carries its covered range.");
        var span = range.EndInclusive.Value - range.StartInclusive.Value + 1;
        return span > int.MaxValue ? int.MaxValue : (int) span;
    }

    private async Task<CompactionActivationResult> ReconcileConflictAsync(
        ISessionCoordinator coordinator,
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence branchTip,
        CompactionManifest manifest,
        SessionAppendConflict conflict,
        CancellationToken cancellationToken)
    {
        var (existing, _) = await FindCommittedRecordAsync(
            coordinator, sessionContext, request, branchTip, null, 1, cancellationToken).ConfigureAwait(false);
        return existing is not null
            ? new CompactionRecordActivated(existing)
            : new CompactionRecordConflict(conflict.ExpectedVersion, conflict.ActualVersion, manifest);
    }

    private async Task<CompactionActivationResult> ReconcileFailedAppendAsync(
        ISessionCoordinator coordinator,
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence branchTip,
        SessionAppendFailed failed,
        CancellationToken cancellationToken)
    {
        var (existing, reconciled) = await FindCommittedRecordAsync(
            coordinator, sessionContext, request, branchTip, null, 1, cancellationToken).ConfigureAwait(false);
        return existing is not null
            ? new CompactionRecordActivated(existing)
            : new CompactionRecordActivationFailed(
                new CompactionFailure(
                    CompactionFailureKind.ActivationFailure,
                    reconciled
                        ? $"{failed.SafeMessage} No record for this compaction was committed."
                        : $"{failed.SafeMessage} The commit state could not be reconciled.",
                    retryable: false,
                    ExtensionData.Empty));
    }

    private async Task<(CompactionRecord? Record, bool Reconciled)> FindCommittedRecordAsync(
        ISessionCoordinator coordinator,
        SessionOperationContext sessionContext,
        CompactionRequest request,
        SessionSequence fromSequenceExclusive,
        SessionReadSnapshot? pinned,
        int maximumPages,
        CancellationToken cancellationToken)
    {
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

            pinned ??= page.Snapshot;
            if (page.Snapshot != pinned)
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

        return (null, true);
    }
}
