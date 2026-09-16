// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>
/// A durable host-local SQLite <see cref="ISessionStore"/> with transactional session semantics.
/// </summary>
/// <remarks>
/// <para>
/// Every session, branch, entry, lane, admission, and idempotency receipt is its own row in the relational
/// schema declared by <see cref="SqliteSessionSchema"/>. Every read runs inside a deferred read transaction and
/// every mutation inside a non-deferred ("<c>BEGIN IMMEDIATE</c>") transaction, so SQLite itself — not a
/// process-local gate — arbitrates writers across every adapter instance sharing the same database file. The
/// adapter is durable on one host and does not claim distributed fencing.
/// </para>
/// <para>
/// Each <see cref="SessionEntry.Sequence"/> is the entry's 1-based position within its own branch: it is that
/// branch's row count at commit time, tracked by a cached tip on the branch's own row so ordinary appends never
/// need to decode any previously committed entry. <see cref="SessionVersion"/> remains the canonical
/// whole-session optimistic-concurrency token and advances once per committed mutation regardless of which
/// branch it targets. Branching copies the parent branch's committed rows up to the fork point into the new
/// branch's own rows without decoding them, so appends to either branch afterward never affect the other and
/// each branch's sequence coordinates are independent of its siblings'. A <see cref="SessionEntryId"/>, not the
/// pair of branch and sequence, is the cross-branch identity of an entry.
/// </para>
/// <para>
/// Exact paged-read continuations require a snapshot previously issued by this adapter instance. At most
/// <see cref="SqliteSessionStoreSettings.MaximumIssuedReadSnapshots"/> distinct snapshots (4,096 by default) are
/// retained in process; eviction or adapter restart fails the continuation instead of trusting caller-authored
/// version and sequence claims.
/// </para>
/// <para>
/// Every entry an operation intends to commit, whether caller-appended or store-authored, is encoded through the
/// captured codec catalog before the operation writes any row. An entry with no durable codec, or whose encoded
/// payload exceeds <see cref="SqliteSessionStoreSettings.MaximumEntryPayloadBytes"/>, is rejected with that
/// operation's typed failure (for example <see cref="SessionAppendFailed"/>) and nothing is persisted. Because a
/// decode failure can now only happen while reading or forking the one branch that actually contains the
/// offending row, a corrupt or unreadable entry affects only operations against its own session — never a scan
/// across the whole store.
/// </para>
/// </remarks>
public sealed partial class SqliteSessionStore: ISessionStore, IDisposable
{
    private readonly Lock _gate = new();
    private readonly HashSet<SessionReadSnapshot> _issuedReadSnapshots = [];
    private readonly Queue<SessionReadSnapshot> _issuedReadSnapshotOrder = [];
    private readonly IIdentifierGenerator<BranchId> _branchIds;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly ISecurityGrantStore _grants;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionEntryCodecCatalog _entryCodecs;
    private readonly SqliteSessionStoreSettings _settings;
    private readonly ILogger<SqliteSessionStore> _logger;
    private readonly SqliteSessionDatabase _database;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SqliteSessionStore"/> class.</summary>
    /// <param name="branchIds">Generates the identity of each newly created branch.</param>
    /// <param name="auditRecordIds">Generates stable identities for required audit intents.</param>
    /// <param name="auditDispatcher">The required audit dispatcher that must accept each consumed access intent.</param>
    /// <param name="grants">The authoritative store that validates and consumes exact session-access grants.</param>
    /// <param name="timeProvider">The clock used to timestamp created and updated sessions.</param>
    /// <param name="entryCodecs">The frozen portable codec catalog used for durable session entries.</param>
    /// <param name="target">The explicit fixed SQLite target and bootstrap modes.</param>
    /// <param name="settings">The finite database operation and serialization bounds.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public SqliteSessionStore(
        IIdentifierGenerator<BranchId> branchIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ISessionEntryCodecCatalog entryCodecs,
        SqliteSessionStoreTarget target,
        SqliteSessionStoreSettings settings,
        ILogger<SqliteSessionStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(branchIds);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(entryCodecs);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);

        _branchIds = branchIds;
        _auditRecordIds = auditRecordIds;
        _auditDispatcher = auditDispatcher;
        _grants = grants;
        _timeProvider = timeProvider;
        _entryCodecs = entryCodecs;
        _settings = settings;
        _database = new SqliteSessionDatabase(target, settings, entryCodecs);
        _logger = logger ?? NullLogger<SqliteSessionStore>.Instance;
    }

    /// <inheritdoc/>
    public SessionStoreDescriptor Descriptor { get; } = new(
        new SessionStoreKey("agentkit.sqlite"),
        SessionStoreCapabilities.Branching | SessionStoreCapabilities.Transactions,
        SessionConsistencyModel.Strong,
        durable: true,
        supportsDistributedFencing: false);

    /// <summary>Marks this store closed without touching persisted state.</summary>
    /// <remarks>Callers must quiesce store operations before disposal; repeated disposal is harmless.</remarks>
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    /// <inheritdoc/>
    private async ValueTask<SessionCreateResult> CreateCoreAsync(
        SqliteSessionUnitOfWork uow, SessionStoreCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var logical = request.Request;
        var tenantId = logical.Identity.TenantId;
        var agentId = logical.AgentId;

        var deletedRequest = await uow.GetDeletedCreateRequestAsync<SessionStoreCreateRequest>(
            tenantId, agentId, logical.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (deletedRequest is not null)
        {
            return deletedRequest.Equals(request)
                ? new SessionCreateFailed("The session created by this idempotency key was deleted.")
                : new SessionCreateFailed("The idempotency key was previously used with different request evidence.");
        }

        var existingReceipt = await uow.GetStoreScopeReceiptAsync<SessionStoreCreateRequest, SessionCreated>(
            SqliteSessionIdempotencyScope.Create, tenantId, agentId, sessionId: null, logical.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingReceipt is not null)
        {
            return existingReceipt.Request.Equals(request)
                ? existingReceipt.Result
                : new SessionCreateFailed("The idempotency key was previously used with different request evidence.");
        }

        if (await uow.GetSessionAsync(request.Address, cancellationToken).ConfigureAwait(false) is not null)
        {
            return new SessionCreateFailed("The allocated session address is already present.");
        }

        var now = _timeProvider.GetUtcNow();
        var branchId = _branchIds.Create();
        var record = new SessionRecord(
            request.Address, logical.ConversationId, tenantId, logical.Identity.PrincipalId, branchId,
            now, now, SessionLifecycleState.Active, 0);
        await uow.InsertSessionAsync(record, branchId, cancellationToken).ConfigureAwait(false);

        var created = new SessionCreated(ToDescriptor(record), existing: false);
        await uow.PutCreateReceiptAsync(tenantId, agentId, logical.IdempotencyKey, request, created, request.Address, cancellationToken)
            .ConfigureAwait(false);
        return created;
    }

    /// <inheritdoc/>
    private async ValueTask<SessionLoadResult> LoadCoreAsync(
        SqliteSessionUnitOfWork uow, SessionOperationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var address = context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        return record is null || record.TenantId != context.Identity.TenantId
            ? new SessionNotFound(address)
            : new SessionLoaded(ToDescriptor(record));
    }

    private async ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneCoreAsync(
        SqliteSessionUnitOfWork uow, SessionExecutionLaneProvisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionExecutionLaneProvisionRejected("The session is unavailable.");
        }

        var replay = await uow.GetSessionScopeReceiptAsync<SessionExecutionLaneProvisionRequest, SessionExecutionLaneProvisioned>(
            SqliteSessionIdempotencyScope.LaneProvision, address, branchId: null, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Request == request
                ? new SessionExecutionLaneProvisioned(
                    replay.Result.ExecutionLaneId, replay.Result.BranchCursor, replay.Result.LaneRevision,
                    replay.Result.SessionVersion, existing: true)
                : new SessionExecutionLaneProvisionConflict("The provisioning idempotency key was reused with different evidence.");
        }

        var laneId = request.Context.ExecutionLaneId!.Value;
        if (await uow.GetLaneAsync(address, laneId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return new SessionExecutionLaneProvisionConflict("The execution lane is already provisioned.");
        }

        if (record.Version != request.ExpectedVersion.Value)
        {
            return new SessionExecutionLaneProvisionConflict("The expected session version is stale.");
        }

        var tip = await uow.GetBranchTipAsync(address, request.BranchCursor.BranchId, cancellationToken).ConfigureAwait(false);
        if (tip is null || new SessionBranchCursor(request.BranchCursor.BranchId, tip.Value.TipEntryId) != request.BranchCursor)
        {
            return new SessionExecutionLaneProvisionConflict("The branch cursor is stale or unavailable.");
        }

        if (await uow.AnyLaneOwnsBranchAsync(address, request.BranchCursor.BranchId, cancellationToken).ConfigureAwait(false))
        {
            return new SessionExecutionLaneProvisionConflict("The branch is already owned by another execution lane.");
        }

        if (await uow.EntryIdReservedAsync(address, request.EntryId, cancellationToken).ConfigureAwait(false))
        {
            return new SessionExecutionLaneProvisionConflict("The provisioning entry identity is already reserved.");
        }

        var laneRevision = new SessionLaneRevision(1);
        var sequence = new SessionSequence(tip.Value.TipSequence + 1);
        var entry = new ExecutionLaneProvisionedSessionEntry(
            request.EntryId, address, (BeforeRunOperationCorrelation) request.Context.Correlation,
            request.BranchCursor.BranchId, sequence, request.BranchCursor.LastEntryId, request.ProvisionedAt,
            new SchemaVersion("1"), laneId, laneRevision, request.SessionProfile, request.Configuration);
        var committedCursor = new SessionBranchCursor(request.BranchCursor.BranchId, request.EntryId);
        var sessionVersion = new SessionVersion(record.Version + 1);
        var result = new SessionExecutionLaneProvisioned(laneId, committedCursor, laneRevision, sessionVersion, existing: false);
        if (!CanPersist([entry], out var codecRejection))
        {
            return new SessionExecutionLaneProvisionRejected(codecRejection);
        }

        await uow.InsertEntryAsync(address, request.BranchCursor.BranchId, entry, cancellationToken).ConfigureAwait(false);
        await uow.InsertLaneAsync(address, laneId, committedCursor, laneRevision, cancellationToken).ConfigureAwait(false);
        await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.LaneProvision, address, branchId: null, request.IdempotencyKey, request, result, cancellationToken)
            .ConfigureAwait(false);
        await uow.UpdateSessionVersionAsync(address, record.Version, sessionVersion.Value, request.ProvisionedAt, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc/>
    private async ValueTask<SessionAppendResult> AppendCoreAsync(
        SqliteSessionUnitOfWork uow, SessionAppendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionAppendNotFound(address);
        }

        var tip = await uow.GetBranchTipAsync(address, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (tip is null)
        {
            return new SessionAppendNotFound(address);
        }

        var cached = await uow.GetSessionScopeReceiptAsync<SessionAppendRequest, SessionAppended>(
            SqliteSessionIdempotencyScope.Append, address, request.BranchId, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return cached.Request.Equals(request)
                ? cached.Result
                : new SessionAppendFailed("The idempotency key was previously used with different request evidence.");
        }

        var currentVersion = new SessionVersion(record.Version);
        if (currentVersion.Value != request.ExpectedVersion.Value)
        {
            return new SessionAppendConflict(request.ExpectedVersion, currentVersion);
        }

        for (var i = 0; i < request.Entries.Length; i++)
        {
            var expectedSequence = tip.Value.TipSequence + i + 1;
            if (request.Entries[i].Sequence.Value != expectedSequence)
            {
                return new SessionAppendFailed(
                    $"Entry at position {i} has sequence {request.Entries[i].Sequence.Value}; expected {expectedSequence}.");
            }
        }

        var proposedEntryIds = request.Entries.Select(static entry => entry.Id).ToArray();
        if (proposedEntryIds.Distinct().Count() != proposedEntryIds.Length
            || await AnyReservedAsync(uow, address, proposedEntryIds, cancellationToken).ConfigureAwait(false))
        {
            return new SessionAppendFailed("An appended entry identity is already reserved.");
        }

        var proposedMessageIds = request.Entries.OfType<MessageSessionEntry>().Select(static entry => entry.Message.Id).ToArray();
        if (proposedMessageIds.Distinct().Count() != proposedMessageIds.Length
            || await AnyMessageReservedAsync(uow, address, proposedMessageIds, cancellationToken).ConfigureAwait(false))
        {
            return new SessionAppendFailed("An appended message identity is already reserved.");
        }

        if (!CanPersist(request.Entries, out var codecRejection))
        {
            return new SessionAppendFailed(codecRejection);
        }

        foreach (var entry in request.Entries)
        {
            await uow.InsertEntryAsync(address, request.BranchId, entry, cancellationToken).ConfigureAwait(false);
        }

        var newVersion = new SessionVersion(record.Version + 1);
        await uow.UpdateSessionVersionAsync(address, record.Version, newVersion.Value, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        await AdvanceOwningLaneCursorAsync(uow, address, request, cancellationToken).ConfigureAwait(false);

        var appended = new SessionAppended(newVersion, request.Entries);
        await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.Append, address, request.BranchId, request.IdempotencyKey, request, appended, cancellationToken)
            .ConfigureAwait(false);
        return appended;
    }

    /// <inheritdoc/>
    private async ValueTask<SessionPageResult> ReadCoreAsync(
        SqliteSessionUnitOfWork uow, SessionReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionReadNotFound(address);
        }

        var tip = await uow.GetBranchTipAsync(address, request.BranchId, cancellationToken).ConfigureAwait(false);
        if (tip is null)
        {
            return new SessionReadNotFound(address);
        }

        var upperSequence = tip.Value.TipSequence;
        if (request.Snapshot is null && request.FromSequenceExclusive.Value > upperSequence)
        {
            return new SessionReadFailed("The requested starting sequence is beyond the current branch tip.");
        }

        if (request.Snapshot is { } supplied)
        {
            bool retained;
            using (_gate.EnterScope())
            {
                retained = _issuedReadSnapshots.Contains(supplied);
            }

            if (!retained || supplied.Version.Value > record.Version || supplied.UpperSequence.Value > upperSequence)
            {
                return new SessionReadFailed("The supplied session read snapshot is not available for this branch.");
            }
        }

        var snapshot = request.Snapshot ?? new SessionReadSnapshot(address, request.BranchId, new SessionVersion(record.Version), new SessionSequence(upperSequence));
        if (request.Snapshot is null)
        {
            RetainIssuedReadSnapshot(snapshot);
        }

        var pageEntries = await uow.ReadEntriesAsync(
            address, request.BranchId, request.FromSequenceExclusive.Value, snapshot.UpperSequence.Value, request.PageSize, cancellationToken)
            .ConfigureAwait(false);
        var throughSequence = pageEntries.IsEmpty ? request.FromSequenceExclusive : pageEntries[^1].Sequence;
        var hasMore = await uow.HasMoreEntriesAsync(
            address, request.BranchId, throughSequence.Value, snapshot.UpperSequence.Value, cancellationToken).ConfigureAwait(false);

        return new SessionPage(pageEntries, throughSequence, hasMore, snapshot);
    }

    /// <summary>Retains bounded adapter-issued provenance for exact continuation snapshots.</summary>
    /// <param name="snapshot">The snapshot created under the serialized read boundary.</param>
    private void RetainIssuedReadSnapshot(SessionReadSnapshot snapshot)
    {
        Debug.Assert(snapshot is not null, "The read path creates a nonnull snapshot before retention.");
        using (_gate.EnterScope())
        {
            if (!_issuedReadSnapshots.Add(snapshot))
            {
                return;
            }

            _issuedReadSnapshotOrder.Enqueue(snapshot);
            if (_issuedReadSnapshotOrder.Count > _settings.MaximumIssuedReadSnapshots)
            {
                _ = _issuedReadSnapshots.Remove(_issuedReadSnapshotOrder.Dequeue());
            }
        }
    }

    /// <inheritdoc/>
    private async ValueTask<SessionBranchResult> CreateBranchCoreAsync(
        SqliteSessionUnitOfWork uow, SessionBranchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        var parentTip = record is null || record.TenantId != request.Context.Identity.TenantId
            ? null
            : await uow.GetBranchTipAsync(address, request.ParentBranchId, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId || parentTip is null)
        {
            return new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence);
        }

        var existingReceipt = await uow.GetSessionScopeReceiptAsync<SessionBranchRequest, SessionBranched>(
            SqliteSessionIdempotencyScope.Branch, address, branchId: null, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingReceipt is not null)
        {
            return existingReceipt.Request.Equals(request)
                ? existingReceipt.Result
                : new SessionBranchFailed("The idempotency key was previously used with different request evidence.");
        }

        if (!await uow.IsCommittedForkPointAsync(address, request.ParentBranchId, request.AtSequence.Value, cancellationToken)
            .ConfigureAwait(false))
        {
            return new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence);
        }

        var newBranchId = _branchIds.Create();
        await uow.InsertBranchAsync(address, newBranchId, request.ParentBranchId, request.AtSequence.Value, cancellationToken)
            .ConfigureAwait(false);
        await uow.CopyEntriesForForkAsync(address, request.ParentBranchId, newBranchId, request.AtSequence.Value, cancellationToken)
            .ConfigureAwait(false);

        var branched = new SessionBranched(newBranchId, request.AtSequence);
        await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.Branch, address, branchId: null, request.IdempotencyKey, request, branched, cancellationToken)
            .ConfigureAwait(false);
        await uow.UpdateSessionVersionAsync(address, record.Version, record.Version + 1, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return branched;
    }

    /// <inheritdoc/>
    private static async ValueTask<SessionDeleteResult> DeleteCoreAsync(
        SqliteSessionUnitOfWork uow, SessionDeleteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var tenantId = request.Context.Identity.TenantId;

        var existingReceipt = await uow.GetStoreScopeReceiptAsync<SessionDeleteRequest, SessionDeleted>(
            SqliteSessionIdempotencyScope.Delete, tenantId, address.AgentId, address.SessionId, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingReceipt is not null)
        {
            return existingReceipt.Request.Equals(request)
                ? existingReceipt.Result
                : new SessionDeleteFailed("The idempotency key was previously used with different request evidence.");
        }

        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.TenantId != tenantId)
        {
            return new SessionDeleted(address);
        }

        if (record is not null)
        {
            await uow.DeleteSessionCascadeAsync(address, cancellationToken).ConfigureAwait(false);
            await uow.MigrateCreateReceiptToDeletedAsync(address, cancellationToken).ConfigureAwait(false);
        }

        var deleted = new SessionDeleted(address);
        await uow.PutDeleteReceiptAsync(tenantId, address, request.IdempotencyKey, request, deleted, cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static async ValueTask<SessionInputLookupResult> LookupInputCoreAsync(
        SqliteSessionUnitOfWork uow, SessionInputLookupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionInputNotFound();
        }

        var stored = await uow.GetAdmissionByInputIdAsync(address, request.Input.Id, cancellationToken).ConfigureAwait(false);
        return stored is null
            ? new SessionInputNotFound()
            : !EquivalentAdmission(stored, request.Context, request.Input, request.OriginalFingerprint)
                ? new SessionInputLookupConflict(request.Input.Id, "The input identity was already admitted with different immutable evidence.")
                : new SessionInputReplayFound(stored.Input, stored.Correlation, ExistingReceipt(stored.Receipt));
    }

    private async ValueTask<InputAdmissionResult> AdmitInputCoreAsync(
        SqliteSessionUnitOfWork uow, SessionInputAdmissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.AddressNotFound, "The session is unavailable."));
        }

        var replay = await uow.GetSessionScopeReceiptAsync<SessionInputAdmissionRequest, AcceptedInput>(
            SqliteSessionIdempotencyScope.Admission, address, branchId: null, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            return SessionStoreSecurityBinding.Fingerprint(replay.Request) == SessionStoreSecurityBinding.Fingerprint(request)
                ? new AcceptedInput(ExistingReceipt(replay.Result.Receipt))
                : new InputConflict(request.OriginalPayload.Id, "The admission idempotency key was reused with different evidence.");
        }

        var stored = await uow.GetAdmissionByInputIdAsync(address, request.OriginalPayload.Id, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            if (!EquivalentAdmission(stored, request.Context, request.OriginalPayload, request.Preprocessing.OriginalFingerprint))
            {
                return new InputConflict(request.OriginalPayload.Id, "The input identity was already admitted with different immutable evidence.");
            }

            var acceptedReplay = new AcceptedInput(ExistingReceipt(stored.Receipt));
            await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.Admission, address, branchId: null, request.IdempotencyKey, request, acceptedReplay,
                cancellationToken).ConfigureAwait(false);
            return acceptedReplay;
        }

        if (record.Version != request.ExpectedVersion.Value)
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.StaleVersion, "The expected session version is stale."));
        }

        var laneId = request.Context.ExecutionLaneId!.Value;
        var lane = await uow.GetLaneAsync(address, laneId, cancellationToken).ConfigureAwait(false);
        if (lane is null)
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.AddressNotFound, "The execution lane is not provisioned."));
        }

        if (lane.Revision != request.ExpectedLaneRevision || lane.BranchCursor != request.BranchCursor)
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.StaleVersion, "The expected lane revision or branch cursor is stale."));
        }

        if (await uow.GetAdmissionByIdAsync(address, request.AdmissionId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return new InputConflict(request.OriginalPayload.Id, "The admission identity is already reserved.");
        }

        if (await uow.EntryIdReservedAsync(address, request.EntryId, cancellationToken).ConfigureAwait(false))
        {
            return new InputConflict(request.OriginalPayload.Id, "The admission entry identity is already reserved.");
        }

        var pendingCount = await uow.CountPendingAdmissionsAsync(address, cancellationToken).ConfigureAwait(false);
        if (pendingCount >= request.MaximumPendingInputs)
        {
            return new QueueCapacityExceeded(new InputCapacityLimit(request.MaximumPendingInputs, pendingCount), retryAfter: null);
        }

        var tip = await uow.GetBranchTipAsync(address, lane.BranchCursor.BranchId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A provisioned lane's owned branch is missing.");
        var sequence = new SessionSequence(tip.TipSequence + 1);
        var admitted = new AdmittedInput(
            request.AdmissionId, request.Context.AgentId, request.Context.SessionId, laneId, request.Context.Identity,
            sequence, request.OriginalPayload, request.EffectivePayload, request.Preprocessing, request.AdmittedAt);
        var entry = new InputAdmittedSessionEntry(
            request.EntryId, address, (BeforeRunOperationCorrelation) request.Context.Correlation, lane.BranchCursor.BranchId,
            sequence, tip.TipEntryId, request.AdmittedAt, new SchemaVersion("1"), admitted);
        var receipt = new AdmissionReceipt(
            admitted.AdmissionId, admitted.OriginalPayload.Id, admitted.AgentId, admitted.SessionId, admitted.ExecutionLaneId,
            admitted.AdmittedSequence, existing: false);
        var retained = new StoredAdmission(admitted, (BeforeRunOperationCorrelation) request.Context.Correlation, request.EntryId, receipt);
        var accepted = new AcceptedInput(receipt);
        if (!CanPersist([entry], out var codecRejection))
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.InvalidInput, codecRejection));
        }

        await uow.InsertAdmissionAsync(address, retained, cancellationToken).ConfigureAwait(false);
        await uow.InsertEntryAsync(address, lane.BranchCursor.BranchId, entry, cancellationToken).ConfigureAwait(false);
        await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.Admission, address, branchId: null, request.IdempotencyKey, request, accepted, cancellationToken)
            .ConfigureAwait(false);
        var newCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, entry.Id);
        var newRevision = new SessionLaneRevision(lane.Revision.Value + 1);
        await uow.UpdateLaneCursorAsync(address, laneId, newCursor, newRevision, cancellationToken).ConfigureAwait(false);
        await uow.UpdateSessionVersionAsync(address, record.Version, record.Version + 1, request.AdmittedAt, cancellationToken)
            .ConfigureAwait(false);
        return accepted;
    }

    private async ValueTask<SessionRunStartResult> AcceptRunCoreAsync(
        SqliteSessionUnitOfWork uow, SessionRunStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionRunStartRejected("The session is unavailable.");
        }

        var replay = await uow.GetSessionScopeReceiptAsync<SessionRunStartRequest, SessionRunAccepted>(
            SqliteSessionIdempotencyScope.RunStart, address, branchId: null, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            return EquivalentStart(replay.Request, request)
                ? new SessionRunAccepted(replay.Result.State, replay.Result.SessionVersion, existing: true)
                : new SessionRunStartConflict(SessionRunStartConflictKind.Idempotency, "The start idempotency key was reused with different evidence.");
        }

        if (request.ExpectedFencingToken is not null)
        {
            return new SessionRunStartFenced("The SQLite store provides host-local coordination and does not accept distributed fences.");
        }

        var laneId = request.Context.ExecutionLaneId!.Value;
        var lane = await uow.GetLaneAsync(address, laneId, cancellationToken).ConfigureAwait(false);
        if (lane is null)
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.LaneRevision, "The selected lane does not exist.");
        }

        if (lane.AcceptedState is { } active)
        {
            return new SessionRunStartBusy(active.Correlation.OperationId, active.Correlation.RunId);
        }

        if (lane.Revision != request.ExpectedLaneRevision)
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.LaneRevision, "The selected lane revision is stale.");
        }

        var tip = await uow.GetBranchTipAsync(address, lane.BranchCursor.BranchId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A provisioned lane's owned branch is missing.");
        var actualCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, tip.TipEntryId);
        if (lane.BranchCursor != request.BranchCursor || actualCursor != request.BranchCursor)
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.BranchCursor, "The selected branch cursor is stale.");
        }

        if (record.Version != request.ExpectedVersion.Value)
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.SessionVersion, "The expected session version is stale.");
        }

        var candidates = new Dictionary<AdmissionId, StoredAdmission>();
        foreach (var admissionId in request.SelectedAdmissionIds)
        {
            if (await uow.GetAdmissionByIdAsync(address, admissionId, cancellationToken).ConfigureAwait(false) is { } admission)
            {
                candidates[admissionId] = admission;
            }
        }

        if (request.SelectedAdmissionIds.Any(admissionId =>
                candidates.TryGetValue(admissionId, out var admission) && admission.Input.Identity != request.Context.Identity))
        {
            return new SessionRunStartConflict(
                SessionRunStartConflictKind.AdmissionIdentity, "Every promoted admission must retain the exact authorized run identity.");
        }

        if (!TrySelectAdmissions(candidates, request, out var selected))
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.PromotionPlan, "The exact promotion plan is no longer eligible.");
        }

        var initiating = selected.FirstOrDefault(stored => stored.Input.AdmissionId == request.InitiatingAdmissionId);
        if (initiating is null || initiating.Correlation != request.Context.Correlation)
        {
            return new SessionRunStartConflict(
                SessionRunStartConflictKind.AdmissionCorrelation, "The initiating admission correlation differs from the proposed run.");
        }

        var reservedEntryIds = request.EntryIds.Insert(0, request.PromotionEntryId).Add(request.AcceptedEntryId);
        if (await AnyReservedAsync(uow, address, reservedEntryIds, cancellationToken).ConfigureAwait(false))
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.PromotionPlan, "A reserved session-entry identity is already in use.");
        }

        if (await AnyMessageReservedAsync(uow, address, request.MessageIds, cancellationToken).ConfigureAwait(false))
        {
            return new SessionRunStartConflict(SessionRunStartConflictKind.PromotionPlan, "A reserved message identity is already in use.");
        }

        var correlation = new InRunOperationCorrelation(request.Context.Correlation.OperationId, request.RunId, request.InitialTurnId);
        var promotionSequence = new SessionSequence(tip.TipSequence + 1);
        var promotionEntry = new InputPromotedSessionEntry(
            request.PromotionEntryId, address, correlation, lane.BranchCursor.BranchId, promotionSequence, tip.TipEntryId,
            request.AcceptedAt, new SchemaVersion("1"), laneId, request.InitiatingAdmissionId, request.PromotionCutoff,
            request.SelectedAdmissionIds);
        var appended = new List<SessionEntry>(selected.Length + 2) { promotionEntry };
        var parent = promotionEntry.Id;
        for (var index = 0; index < selected.Length; index++)
        {
            var stored = selected[index];
            var entrySequence = new SessionSequence(tip.TipSequence + index + 2);
            var message = new UserMessage(
                request.MessageIds[index], request.Context.AgentId, request.Context.SessionId, record.ConversationId,
                lane.BranchCursor.BranchId, request.RunId, request.InitialTurnId, request.AcceptedAt, MessageState.Complete,
                stored.Input.EffectivePayload.Parts, stored.Input.EffectivePayload.Extensions);
            var entry = new MessageSessionEntry(
                request.EntryIds[index], address, correlation, lane.BranchCursor.BranchId, entrySequence, parent,
                request.AcceptedAt, new SchemaVersion("1"), message);
            appended.Add(entry);
            parent = entry.Id;
        }

        var acceptedSequence = new SessionSequence(tip.TipSequence + selected.Length + 2);
        var committedCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, request.AcceptedEntryId);
        var installedLaneRevision = new SessionLaneRevision(lane.Revision.Value + 1);
        var state = new SessionAcceptedRunState(
            address, laneId, installedLaneRevision, correlation, request.OperationStateRevision, request.Context.Identity,
            request.InRunAuthorization, request.SessionProfile, request.Configuration, request.BranchCursor, committedCursor,
            request.PromotionCutoff, request.InitiatingAdmissionId, request.SelectedAdmissionIds, request.EntryIds,
            request.MessageIds, request.InitialTurnId, request.AcceptedAt);
        appended.Add(new OperationAcceptedSessionEntry(
            request.AcceptedEntryId, address, correlation, lane.BranchCursor.BranchId, acceptedSequence, parent,
            request.AcceptedAt, new SchemaVersion("1"), state));
        if (!CanPersist(appended, out var codecRejection))
        {
            return new SessionRunStartRejected(codecRejection);
        }

        foreach (var entry in appended)
        {
            await uow.InsertEntryAsync(address, lane.BranchCursor.BranchId, entry, cancellationToken).ConfigureAwait(false);
        }

        foreach (var stored in selected)
        {
            var updated = new AdmittedInput(
                stored.Input.AdmissionId, stored.Input.AgentId, stored.Input.SessionId, stored.Input.ExecutionLaneId,
                stored.Input.Identity, stored.Input.AdmittedSequence, stored.Input.OriginalPayload, stored.Input.EffectivePayload,
                stored.Input.Preprocessing, stored.Input.AdmittedAt, promotionSequence);
            await uow.MarkAdmissionPromotedAsync(address, updated, cancellationToken).ConfigureAwait(false);
        }

        await uow.UpdateLaneAcceptedStateAsync(address, laneId, committedCursor, installedLaneRevision, state, cancellationToken)
            .ConfigureAwait(false);
        await uow.UpdateSessionVersionAsync(address, record.Version, record.Version + 1, request.AcceptedAt, cancellationToken)
            .ConfigureAwait(false);
        var result = new SessionRunAccepted(state, new SessionVersion(record.Version + 1), existing: false);
        await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.RunStart, address, branchId: null, request.IdempotencyKey, request, result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    private static async ValueTask<SessionRunStateResult> LoadRunStateCoreAsync(
        SqliteSessionUnitOfWork uow, SessionRunStateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionRunStateUnavailable("The requested operation state is unavailable.");
        }

        var lane = await uow.GetLaneAsync(address, request.Context.ExecutionLaneId!.Value, cancellationToken).ConfigureAwait(false);
        return lane?.AcceptedState is not { } state
            || state.Correlation != request.Context.Correlation
            || state.Identity != request.Context.Identity
            ? new SessionRunStateUnavailable("The requested operation state is unavailable.")
            : new SessionRunStateLoaded(state);
    }

    /// <summary>Atomically clears one lane's installed accepted run state, or reconciles a repeated identical release.</summary>
    /// <param name="uow">The transaction-scoped repository.</param>
    /// <param name="request">The exact protected release request naming the lane and the accepted run it owns.</param>
    /// <param name="cancellationToken">Cancels before the atomic mutation begins.</param>
    /// <returns>The released receipt or a typed rejection.</returns>
    /// <remarks>
    /// A missing session, missing lane, and cross-tenant caller are all masked as
    /// <see cref="SessionRunReleaseRejectionKind.LaneNotFound"/> so an unauthorized caller cannot distinguish
    /// absence from denial. A lane whose installed accepted run does not match this request's exact operation,
    /// run, and state revision is <see cref="SessionRunReleaseRejectionKind.Fenced"/>: a stale caller — for
    /// example a lease left over from a superseded attempt — can never clear a different, newer occupant.
    /// Release appends no session entry, so it requires no codec preflight.
    /// </remarks>
    private async ValueTask<SessionRunReleaseResult> ReleaseRunCoreAsync(
        SqliteSessionUnitOfWork uow, SessionRunReleaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var address = request.Context.ToAddress();
        var record = await uow.GetSessionAsync(address, cancellationToken).ConfigureAwait(false);
        if (record is null || record.TenantId != request.Context.Identity.TenantId)
        {
            return new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.LaneNotFound, "The session is unavailable.");
        }

        var replay = await uow.GetSessionScopeReceiptAsync<SessionRunReleaseRequest, SessionRunReleased>(
            SqliteSessionIdempotencyScope.RunRelease, address, branchId: null, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Request.Equals(request)
                ? new SessionRunReleased(replay.Result.NewVersion, existing: true)
                : new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Idempotency, "The release idempotency key was reused with different evidence.");
        }

        var lane = await uow.GetLaneAsync(address, request.ExecutionLaneId, cancellationToken).ConfigureAwait(false);
        if (lane is null)
        {
            return new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.LaneNotFound, "The selected lane does not exist.");
        }

        if (lane.AcceptedState is not { } active)
        {
            return new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.NoAcceptedRun, "The selected lane holds no accepted run.");
        }

        if (active.Correlation.OperationId != request.OperationId
            || active.Correlation.RunId != request.RunId
            || active.OperationStateRevision != request.ExpectedStateRevision)
        {
            return new SessionRunReleaseRejected(
                SessionRunReleaseRejectionKind.Fenced,
                "The lane's installed accepted run does not match the requested operation, run, and state revision.");
        }

        if (record.Version != request.ExpectedVersion.Value)
        {
            return new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.SessionVersion, "The expected session version is stale.");
        }

        await uow.UpdateLaneAcceptedStateAsync(address, request.ExecutionLaneId, lane.BranchCursor, lane.Revision, null, cancellationToken)
            .ConfigureAwait(false);
        await uow.UpdateSessionVersionAsync(address, record.Version, record.Version + 1, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        var released = new SessionRunReleased(new SessionVersion(record.Version + 1), existing: false);
        await uow.PutSessionScopeReceiptAsync(
            SqliteSessionIdempotencyScope.RunRelease, address, branchId: null, request.IdempotencyKey, request, released, cancellationToken)
            .ConfigureAwait(false);
        return released;
    }

    /// <summary>
    /// Proves every proposed entry has a durable codec and encodes within
    /// <see cref="SqliteSessionStoreSettings.MaximumEntryPayloadBytes"/> before any row is written.
    /// </summary>
    /// <param name="entries">The complete ordered entries the operation intends to commit.</param>
    /// <param name="safeReason">
    /// The content-free rejection reason when an entry cannot be encoded or its encoded payload exceeds the
    /// configured bound; otherwise null.
    /// </param>
    /// <returns><see langword="true"/> when the captured codec catalog encodes every entry within the payload bound.</returns>
    /// <remarks>
    /// Each entry is encoded exactly once here; the same encoded bytes are reused by
    /// <see cref="SqliteSessionUnitOfWork.InsertEntryAsync"/> without re-encoding. Without this preflight an
    /// unencodable or oversized entry would surface as a database exception after some of a batch's rows had
    /// already been written, instead of the typed failure each store operation promises.
    /// </remarks>
    private bool CanPersist(IReadOnlyList<SessionEntry> entries, [NotNullWhen(false)] out string? safeReason)
    {
        Debug.Assert(entries is not null, "Operations preflight a materialized entry collection.");
        for (var index = 0; index < entries.Count; index++)
        {
            switch (_entryCodecs.Encode(entries[index]))
            {
                case SessionEntryEncoded { Wire.Payload.Length: var length } when length > _settings.MaximumEntryPayloadBytes:
                    safeReason = $"Entry at position {index} encodes to {length} bytes; the selected session store accepts at most {_settings.MaximumEntryPayloadBytes}.";
                    return false;
                case SessionEntryEncoded:
                    continue;
                case SessionEntryEncodeRejected rejected:
                    safeReason = $"Entry at position {index} has no durable codec in the selected session store: {rejected.Reason}";
                    return false;
                default:
                    safeReason = $"Entry at position {index} has no durable codec in the selected session store.";
                    return false;
            }
        }

        safeReason = null;
        return true;
    }

    private static async ValueTask<bool> AnyReservedAsync(
        SqliteSessionUnitOfWork uow, SessionAddress address, IEnumerable<SessionEntryId> entryIds, CancellationToken cancellationToken)
    {
        foreach (var entryId in entryIds)
        {
            if (await uow.EntryIdReservedAsync(address, entryId, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<bool> AnyMessageReservedAsync(
        SqliteSessionUnitOfWork uow, SessionAddress address, IEnumerable<MessageId> messageIds, CancellationToken cancellationToken)
    {
        foreach (var messageId in messageIds)
        {
            if (await uow.MessageIdReservedAsync(address, messageId, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EquivalentAdmission(StoredAdmission stored, SessionOperationContext context,
        AgentInput original, InputFingerprint originalFingerprint)
    {
        Debug.Assert(stored is not null, "A retained admission is required.");
        Debug.Assert(context is not null, "A validated context is required.");
        Debug.Assert(original is not null, "An original input is required.");
        return stored.Input.AgentId == context.AgentId
            && stored.Input.SessionId == context.SessionId
            && stored.Input.ExecutionLaneId == context.ExecutionLaneId
            && stored.Input.Identity == context.Identity
            && stored.Correlation == context.Correlation
            && stored.Input.OriginalPayload.Id == original.Id
            && stored.Input.OriginalPayload.Delivery == original.Delivery
            && stored.Input.OriginalPayload.Parts.SequenceEqual(original.Parts)
            && stored.Input.OriginalPayload.Extensions == original.Extensions
            && stored.Input.Preprocessing.OriginalFingerprint == originalFingerprint;
    }

    private static AdmissionReceipt ExistingReceipt(AdmissionReceipt receipt)
    {
        Debug.Assert(receipt is not null, "A retained admission receipt is required.");
        return new AdmissionReceipt(receipt.AdmissionId, receipt.InputId, receipt.AgentId, receipt.SessionId,
            receipt.ExecutionLaneId, receipt.AdmittedSequence, existing: true);
    }

    /// <summary>Moves the appending lane's cursor to the new branch tip when the lane owns the appended branch.</summary>
    /// <param name="uow">The transaction-scoped repository.</param>
    /// <param name="address">The addressed session.</param>
    /// <param name="request">The committed append whose context names the appending lane.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Only the lane named by <see cref="SessionOperationContext.ExecutionLaneId"/> advances, and only when its cursor
    /// is bound to <see cref="SessionAppendRequest.BranchId"/>. Session-wide appends without a lane and appends to a
    /// branch owned by a different lane leave every lane cursor untouched. The lane revision is not changed; later
    /// admission or acceptance still validates the cursor against the real branch tip.
    /// </remarks>
    private static async ValueTask AdvanceOwningLaneCursorAsync(
        SqliteSessionUnitOfWork uow, SessionAddress address, SessionAppendRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(!request.Entries.IsEmpty, "Append validation rejects empty batches before commit.");
        if (request.Context.ExecutionLaneId is not { } laneId)
        {
            return;
        }

        var lane = await uow.GetLaneAsync(address, laneId, cancellationToken).ConfigureAwait(false);
        if (lane is not null && lane.BranchCursor.BranchId == request.BranchId)
        {
            var cursor = new SessionBranchCursor(request.BranchId, request.Entries[^1].Id);
            await uow.UpdateLaneCursorAsync(address, laneId, cursor, lane.Revision, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TrySelectAdmissions(
        Dictionary<AdmissionId, StoredAdmission> candidates, SessionRunStartRequest request,
        out ImmutableArray<StoredAdmission> selected)
    {
        Debug.Assert(request is not null, "A validated start request is required.");
        var builder = ImmutableArray.CreateBuilder<StoredAdmission>(request.SelectedAdmissionIds.Length);
        foreach (var admissionId in request.SelectedAdmissionIds)
        {
            if (!candidates.TryGetValue(admissionId, out var stored)
                || stored.Input.PromotedSequence is not null
                || stored.Input.ExecutionLaneId != request.Context.ExecutionLaneId
                || stored.Input.Identity != request.Context.Identity
                || stored.Input.AdmittedSequence.Value > request.PromotionCutoff.Value)
            {
                selected = [];
                return false;
            }
            builder.Add(stored);
        }

        selected = builder.MoveToImmutable();
        return selected.Select(static value => value.Input.AdmittedSequence.Value).SequenceEqual(
                selected.Select(static value => value.Input.AdmittedSequence.Value).Order());
    }

    private static bool EquivalentStart(SessionRunStartRequest left, SessionRunStartRequest right)
    {
        Debug.Assert(left is not null && right is not null, "Start requests are required.");
        return left.Context == right.Context
            && left.InitiatingAdmissionId == right.InitiatingAdmissionId
            && left.SelectedAdmissionIds.SequenceEqual(right.SelectedAdmissionIds)
            && left.PromotionCutoff == right.PromotionCutoff
            && left.ExpectedLaneRevision == right.ExpectedLaneRevision
            && left.ExpectedVersion == right.ExpectedVersion
            && left.BranchCursor == right.BranchCursor
            && left.ExpectedFencingToken == right.ExpectedFencingToken
            && left.RunId == right.RunId
            && left.InitialTurnId == right.InitialTurnId
            && left.PromotionEntryId == right.PromotionEntryId
            && left.EntryIds.SequenceEqual(right.EntryIds)
            && left.MessageIds.SequenceEqual(right.MessageIds)
            && left.AcceptedEntryId == right.AcceptedEntryId
            && left.OperationStateRevision == right.OperationStateRevision
            && left.SessionProfile == right.SessionProfile
            && left.Configuration == right.Configuration
            && left.InRunAuthorization == right.InRunAuthorization
            && left.AcceptedAt == right.AcceptedAt;
    }

    private SessionDescriptor ToDescriptor(SessionRecord record) => new(
        record.Address,
        record.ConversationId,
        record.TenantId,
        record.OwnerId,
        Descriptor.Key,
        record.ActiveBranchId,
        new SessionVersion(record.Version),
        record.State,
        record.CreatedAt,
        record.UpdatedAt,
        new SchemaVersion("1"),
        ExtensionData.Empty);
}
