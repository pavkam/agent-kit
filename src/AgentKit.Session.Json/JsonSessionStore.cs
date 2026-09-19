// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Persists one complete session record as a newline-delimited JSON transition log over a fixed local root.</summary>
/// <remarks>
/// <para>
/// The store is log structured. Every accepted mutation is encoded as exactly one JSON line that is appended and flushed to
/// disk <em>before</em> the in-memory projection changes, so an acknowledged effect survives process loss and a failed write
/// leaves the projection untouched. A line carries the exact immutable request plus the values this adapter generated for
/// that commit, which makes the whole batch — entries, promoted admissions, materialized messages, installed accepted run
/// state — atomic by construction.
/// </para>
/// <para>
/// <see cref="InitializeAsync"/> replays the log through the same deterministic commit path that produced it, so session
/// versions, per-branch sequence spaces, per-lane cursors and revisions, installed accepted runs, and every idempotency
/// receipt are reproduced exactly. Optimistic concurrency therefore survives a restart: a caller holding a version observed
/// before the restart still conflicts or commits exactly as it would have before.
/// </para>
/// <para>
/// All operations are serialized by one store-wide gate, which makes the read-decide-append-mutate sequence linearizable
/// within the process. A host-local advisory exclusive lock is held for the store's lifetime, so a second writer on the
/// same host fails fast instead of interleaving appends. This is durable single-process host-local storage: it provides no
/// distributed lease and no fencing token, so <see cref="SessionStoreDescriptor.SupportsDistributedFencing"/> is
/// <see langword="false"/> and a run start presenting a fence is rejected.
/// </para>
/// <para>
/// Exact paged-read continuations require a snapshot this instance issued. Issued snapshots are bounded process-local
/// provenance rather than durable state: they are dropped on restart and evicted in first-in, first-out order beyond
/// <see cref="JsonSessionStoreSettings.MaximumIssuedReadSnapshots"/>, and an unavailable snapshot fails as a typed read
/// failure instead of being trusted as caller-authored evidence.
/// </para>
/// <para>
/// Call <see cref="InitializeAsync"/> exactly once during trusted bootstrap before resolving the store for use.
/// </para>
/// </remarks>
public sealed partial class JsonSessionStore: ISessionStore, IDisposable
{
    private const string _storeKind = "agentkit.session.store";
    private const string _logName = "sessions";
    private const int _schemaVersion = 1;

    private readonly Lock _gate = new();
    private readonly HashSet<SessionReadSnapshot> _issuedReadSnapshots = [];
    private readonly Queue<SessionReadSnapshot> _issuedReadSnapshotOrder = [];
    private readonly Dictionary<SessionAddress, SessionRecord> _sessions = [];
    private readonly Dictionary<(TenantId TenantId, AgentId AgentId, IdempotencyKey Key), IdempotencyReceipt<SessionStoreCreateRequest, SessionCreated>> _createIdempotency = [];
    private readonly Dictionary<(TenantId TenantId, AgentId AgentId, IdempotencyKey Key), SessionStoreCreateRequest> _deletedCreateIdempotency = [];
    private readonly Dictionary<(TenantId TenantId, SessionAddress Address, IdempotencyKey Key), IdempotencyReceipt<SessionDeleteRequest, SessionDeleted>> _deleteIdempotency = [];
    private readonly IIdentifierGenerator<BranchId> _branchIds;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly ISecurityGrantStore _grants;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonSessionStore> _logger;
    private readonly JsonSessionStoreTarget _target;
    private readonly JsonSessionStoreSettings _settings;
    private readonly JsonEncodingSettings _recordEncoding;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _replaying;
    private bool _disposed;

    /// <summary>Initializes a store for one host-authorized fixed root without opening, creating, or locking it.</summary>
    /// <param name="branchIds">Generates the identity of the initial branch and of every later fork.</param>
    /// <param name="auditRecordIds">Generates stable identities for required audit intents.</param>
    /// <param name="auditDispatcher">The required audit dispatcher that must accept each consumed access intent.</param>
    /// <param name="grants">The authoritative store that validates and consumes exact session-access grants.</param>
    /// <param name="timeProvider">The clock used to stamp creation, append, fork, and release times.</param>
    /// <param name="entryCodecs">The frozen portable entry codecs used for every durable session-entry payload.</param>
    /// <param name="target">The exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable bounds, continuation limits, compaction policy, and encoding contract.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    /// <remarks>
    /// Construction performs no I/O, so composition never touches the filesystem; every declared effect happens in
    /// <see cref="InitializeAsync"/>. The effective record contract is derived here by layering this leaf's required
    /// polymorphism resolver, identity converters, and entry-envelope converter onto the configured encoding, and its
    /// fingerprint is what the manifest binds.
    /// </remarks>
    public JsonSessionStore(
        IIdentifierGenerator<BranchId> branchIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ISessionEntryCodecCatalog entryCodecs,
        JsonSessionStoreTarget target,
        JsonSessionStoreSettings settings,
        ILogger<JsonSessionStore>? logger = null)
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
        _target = target;
        _settings = settings;
        _logger = logger ?? NullLogger<JsonSessionStore>.Instance;
        _recordEncoding = JsonSessionSerialization.CreateStoreRecordEncoding(settings.Encoding, entryCodecs);
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <inheritdoc/>
    /// <value>
    /// A durable, strongly consistent branching and transactional store keyed <c>agentkit.json</c>. Distributed fencing is
    /// not advertised because a host-local advisory lock proves nothing about another host.
    /// </value>
    public SessionStoreDescriptor Descriptor { get; } = new(
        new SessionStoreKey("agentkit.json"),
        SessionStoreCapabilities.Branching | SessionStoreCapabilities.Transactions,
        SessionConsistencyModel.Strong,
        durable: true,
        supportsDistributedFencing: false);

    /// <summary>Validates or creates the store root, binds its encoding contract, and replays committed state into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for session operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, store identity, encoding contract, or persisted evidence cannot be validated safely.</exception>
    /// <exception cref="IOException">The root or its record log cannot be read or written.</exception>
    /// <remarks>
    /// <para>
    /// Initialization acquires the advisory exclusive lock first, so a concurrent writer is rejected before any validation
    /// observes racing state. It then verifies the manifest's store identity, schema version, and encoding fingerprint, and
    /// performs a round-trip self-check proving the configured contract can reproduce this store's evidence.
    /// </para>
    /// <para>
    /// A log ending in an incomplete append is discarded only under <see cref="JsonStoreRecoveryMode.RecoverTornAppends"/>;
    /// under <see cref="JsonStoreRecoveryMode.ValidateExact"/> it is reported as corrupt evidence. Repeating initialization
    /// after success is a no-op.
    /// </para>
    /// </remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (_initialized)
            {
                return ValueTask.CompletedTask;
            }

            _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
            JsonStoreRoot.ValidateFile(_root.ManifestPath);
            JsonStoreRoot.ValidateFile(_log.Path);
            _exclusive = JsonStoreLock.Acquire(_root.LockPath);
            cancellationToken.ThrowIfCancellationRequested();
            JsonStoreSerialization.VerifyRoundTrip(JsonSessionStoreProbe.Create(), _recordEncoding.RecordOptions);
            cancellationToken.ThrowIfCancellationRequested();
            BindManifest(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Replay(cancellationToken);
            _initialized = true;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the advisory exclusive lock held for this store's lifetime and drops the projection.</summary>
    /// <remarks>
    /// Disposal is idempotent and does not flush: every acknowledged record was already flushed to disk when it was
    /// appended. Projected state and issued read snapshots are dropped, so a disposed store cannot serve further
    /// operations and a continuation captured from it is no longer honored.
    /// </remarks>
    public void Dispose()
    {
        using (_gate.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initialized = false;
            _sessions.Clear();
            _createIdempotency.Clear();
            _deletedCreateIdempotency.Clear();
            _deleteIdempotency.Clear();
            _issuedReadSnapshots.Clear();
            _issuedReadSnapshotOrder.Clear();
            _exclusive?.Dispose();
            _exclusive = null;
        }
    }

    private SessionCreateResult CreateCore(
        SessionStoreCreateRequest request, BranchId branchId, DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the creation request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var logical = request.Request;
            var idempotencyEntry = (logical.Identity.TenantId, logical.AgentId, logical.IdempotencyKey);
            if (_deletedCreateIdempotency.TryGetValue(idempotencyEntry, out var deletedRequest))
            {
                return deletedRequest.Equals(request)
                    ? new SessionCreateFailed("The session created by this idempotency key was deleted.")
                    : new SessionCreateFailed("The idempotency key was previously used with different request evidence.");
            }

            if (_createIdempotency.TryGetValue(idempotencyEntry, out var existingReceipt))
            {
                return existingReceipt.Request.Equals(request)
                    ? existingReceipt.Result
                    : new SessionCreateFailed("The idempotency key was previously used with different request evidence.");
            }

            if (_sessions.ContainsKey(request.Address))
            {
                return new SessionCreateFailed("The allocated session address is already present.");
            }

            Persist(JsonSessionStoreLogRecord.ForCreate(request, branchId, createdAt), cancellationToken);
            var record = new SessionRecord(
                request.Address, logical.ConversationId, logical.Identity.TenantId, logical.Identity.PrincipalId,
                branchId, createdAt);
            record.Branches[branchId] = new BranchRecord();
            _sessions[request.Address] = record;
            var created = new SessionCreated(ToDescriptor(record), existing: false);
            _createIdempotency[idempotencyEntry] =
                new IdempotencyReceipt<SessionStoreCreateRequest, SessionCreated>(request, created);
            return created;
        }
    }

    private SessionLoadResult LoadCore(SessionOperationContext context, CancellationToken cancellationToken)
    {
        Debug.Assert(context is not null, "The public boundary validates the operation context.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var address = context.ToAddress();
            return !_sessions.TryGetValue(address, out var record) || record.TenantId != context.Identity.TenantId
                ? new SessionNotFound(address)
                : new SessionLoaded(ToDescriptor(record));
        }
    }

    private SessionExecutionLaneProvisionResult ProvisionLaneCore(
        SessionExecutionLaneProvisionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the provisioning request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return new SessionExecutionLaneProvisionRejected("The session is unavailable.");
            }

            if (record.LaneProvisionIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return replay.Request == request
                    ? new SessionExecutionLaneProvisioned(replay.Result.ExecutionLaneId, replay.Result.BranchCursor,
                        replay.Result.LaneRevision, replay.Result.SessionVersion, existing: true)
                    : new SessionExecutionLaneProvisionConflict(
                        "The provisioning idempotency key was reused with different evidence.");
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (record.Lanes.ContainsKey(laneId))
            {
                return new SessionExecutionLaneProvisionConflict("The execution lane is already provisioned.");
            }
            if (record.Version != request.ExpectedVersion.Value)
            {
                return new SessionExecutionLaneProvisionConflict("The expected session version is stale.");
            }
            if (!record.Branches.TryGetValue(request.BranchCursor.BranchId, out var branch)
                || BranchCursor(request.BranchCursor.BranchId, branch) != request.BranchCursor)
            {
                return new SessionExecutionLaneProvisionConflict("The branch cursor is stale or unavailable.");
            }
            if (record.Lanes.Values.Any(lane => lane.BranchCursor.BranchId == request.BranchCursor.BranchId))
            {
                return new SessionExecutionLaneProvisionConflict("The branch is already owned by another execution lane.");
            }
            if (record.EntryIds.Contains(request.EntryId))
            {
                return new SessionExecutionLaneProvisionConflict("The provisioning entry identity is already reserved.");
            }

            var laneRevision = new SessionLaneRevision(1);
            var sequence = new SessionSequence(branch.Entries.Count + 1);
            var entry = new ExecutionLaneProvisionedSessionEntry(
                request.EntryId, request.Context.ToAddress(), (BeforeRunOperationCorrelation) request.Context.Correlation,
                request.BranchCursor.BranchId, sequence, request.BranchCursor.LastEntryId, request.ProvisionedAt,
                new SchemaVersion("1"), laneId, laneRevision, request.SessionProfile, request.Configuration);
            var committedCursor = new SessionBranchCursor(request.BranchCursor.BranchId, request.EntryId);
            var sessionVersion = new SessionVersion(record.Version + 1);
            var result = new SessionExecutionLaneProvisioned(
                laneId, committedCursor, laneRevision, sessionVersion, existing: false);

            Persist(JsonSessionStoreLogRecord.ForProvision(request), cancellationToken);
            branch.Entries.Add(entry);
            _ = record.EntryIds.Add(entry.Id);
            record.Lanes.Add(laneId, new LaneRecord(committedCursor, laneRevision));
            record.LaneProvisionIdempotency.Add(request.IdempotencyKey,
                new IdempotencyReceipt<SessionExecutionLaneProvisionRequest, SessionExecutionLaneProvisioned>(request, result));
            record.Version = sessionVersion.Value;
            record.UpdatedAt = request.ProvisionedAt;
            return result;
        }
    }

    private SessionAppendResult AppendCore(
        SessionAppendRequest request, DateTimeOffset committedAt, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the append request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.BranchId, out var branch))
            {
                return new SessionAppendNotFound(address);
            }

            if (branch.AppendIdempotency.TryGetValue(request.IdempotencyKey, out var cached))
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

            for (var index = 0; index < request.Entries.Length; index++)
            {
                var entry = request.Entries[index];
                if (entry.Address != address)
                {
                    return new SessionAppendFailed(
                        $"Entry at position {index} declares address '{entry.Address}'; expected '{address}'.");
                }
                if (entry.BranchId != request.BranchId)
                {
                    return new SessionAppendFailed(
                        $"Entry at position {index} declares branch '{entry.BranchId}'; expected '{request.BranchId}'.");
                }

                var expectedSequence = branch.Entries.Count + index + 1;
                if (entry.Sequence.Value != expectedSequence)
                {
                    return new SessionAppendFailed(
                        $"Entry at position {index} has sequence {entry.Sequence.Value}; expected {expectedSequence}.");
                }
            }

            var proposedEntryIds = request.Entries.Select(static entry => entry.Id).ToArray();
            if (proposedEntryIds.Distinct().Count() != proposedEntryIds.Length
                || proposedEntryIds.Any(record.EntryIds.Contains))
            {
                return new SessionAppendFailed("An appended entry identity is already reserved.");
            }

            var proposedMessageIds = request.Entries
                .OfType<MessageSessionEntry>()
                .Select(static entry => entry.Message.Id)
                .ToArray();
            if (proposedMessageIds.Distinct().Count() != proposedMessageIds.Length
                || proposedMessageIds.Any(record.MessageIds.Contains))
            {
                return new SessionAppendFailed("An appended message identity is already reserved.");
            }

            Persist(JsonSessionStoreLogRecord.ForAppend(request, committedAt), cancellationToken);
            branch.Entries.AddRange(request.Entries);
            record.EntryIds.UnionWith(proposedEntryIds);
            record.MessageIds.UnionWith(proposedMessageIds);
            record.Version++;
            record.UpdatedAt = committedAt;
            AdvanceOwningLaneCursor(record, request);

            var appended = new SessionAppended(new SessionVersion(record.Version), request.Entries);
            branch.AppendIdempotency[request.IdempotencyKey] =
                new IdempotencyReceipt<SessionAppendRequest, SessionAppended>(request, appended);
            return appended;
        }
    }

    private SessionPageResult ReadCore(SessionReadRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the read request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.BranchId, out var branch))
            {
                return new SessionReadNotFound(address);
            }

            var upperSequence = branch.Entries.Count == 0
                ? new SessionSequence(0)
                : branch.Entries[^1].Sequence;
            if (request.Snapshot is null && request.FromSequenceExclusive.Value > upperSequence.Value)
            {
                return new SessionReadFailed("The requested starting sequence is beyond the current branch tip.");
            }
            if (request.Snapshot is { } supplied
                && (!_issuedReadSnapshots.Contains(supplied)
                    || supplied.Version.Value > record.Version
                    || supplied.UpperSequence.Value > upperSequence.Value))
            {
                return new SessionReadFailed("The supplied session read snapshot is not available for this branch.");
            }

            var snapshot = request.Snapshot ?? new SessionReadSnapshot(
                address, request.BranchId, new SessionVersion(record.Version), upperSequence);
            if (request.Snapshot is null)
            {
                RetainIssuedReadSnapshot(snapshot);
            }

            var pageEntries = branch.Entries
                .Where(entry => entry.Sequence.Value > request.FromSequenceExclusive.Value
                    && entry.Sequence.Value <= snapshot.UpperSequence.Value)
                .Take(request.PageSize)
                .ToImmutableArray();
            var throughSequence = pageEntries.IsEmpty ? request.FromSequenceExclusive : pageEntries[^1].Sequence;
            var hasMore = branch.Entries.Any(entry => entry.Sequence.Value > throughSequence.Value
                && entry.Sequence.Value <= snapshot.UpperSequence.Value);
            return new SessionPage(pageEntries, throughSequence, hasMore, snapshot);
        }
    }

    private SessionBranchResult CreateBranchCore(
        SessionBranchRequest request, BranchId newBranchId, DateTimeOffset committedAt,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the branch request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.ParentBranchId, out var parentBranch))
            {
                return new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence);
            }

            if (record.BranchIdempotency.TryGetValue(request.IdempotencyKey, out var existingReceipt))
            {
                return existingReceipt.Request.Equals(request)
                    ? existingReceipt.Result
                    : new SessionBranchFailed("The idempotency key was previously used with different request evidence.");
            }
            if (!IsCommittedForkPoint(parentBranch, request.AtSequence))
            {
                return new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence);
            }

            Persist(JsonSessionStoreLogRecord.ForBranch(request, newBranchId, committedAt), cancellationToken);
            var newBranch = new BranchRecord();
            newBranch.Entries.AddRange(
                parentBranch.Entries.Where(entry => entry.Sequence.Value <= request.AtSequence.Value));
            record.Branches[newBranchId] = newBranch;
            var branched = new SessionBranched(newBranchId, request.AtSequence);
            record.BranchIdempotency[request.IdempotencyKey] =
                new IdempotencyReceipt<SessionBranchRequest, SessionBranched>(request, branched);
            record.UpdatedAt = committedAt;
            record.Version++;
            return branched;
        }
    }

    private SessionDeleteResult DeleteCore(SessionDeleteRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the deletion request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            var idempotencyEntry = (request.Context.Identity.TenantId, address, request.IdempotencyKey);
            if (_deleteIdempotency.TryGetValue(idempotencyEntry, out var existingReceipt))
            {
                return existingReceipt.Request.Equals(request)
                    ? existingReceipt.Result
                    : new SessionDeleteFailed("The idempotency key was previously used with different request evidence.");
            }

            if (_sessions.TryGetValue(address, out var record)
                && record.TenantId != request.Context.Identity.TenantId)
            {
                return new SessionDeleted(address);
            }

            Persist(JsonSessionStoreLogRecord.ForDelete(request), cancellationToken);
            if (_sessions.Remove(address))
            {
                RetireCreateReceipt(address);
            }

            var deleted = new SessionDeleted(address);
            _deleteIdempotency[idempotencyEntry] =
                new IdempotencyReceipt<SessionDeleteRequest, SessionDeleted>(request, deleted);
            return deleted;
        }
    }

    private SessionInputLookupResult LookupInputCore(
        SessionInputLookupRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the lookup request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            return !TryGetAuthorizedRecord(request.Context, out var record)
                    || !record.AdmissionsByInput.TryGetValue(request.Input.Id, out var stored)
                ? new SessionInputNotFound()
                : !EquivalentAdmission(stored, request.Context, request.Input, request.OriginalFingerprint)
                ? new SessionInputLookupConflict(request.Input.Id,
                    "The input identity was already admitted with different immutable evidence.")
                : new SessionInputReplayFound(stored.Input, stored.Correlation, ExistingReceipt(stored.Receipt));
        }
    }

    private InputAdmissionResult AdmitInputCore(
        SessionInputAdmissionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the admission request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return new RejectedInput(
                    new InputRejection(InputRejectionKind.AddressNotFound, "The session is unavailable."));
            }

            if (record.AdmissionIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return SessionStoreSecurityBinding.Fingerprint(replay.Request)
                        == SessionStoreSecurityBinding.Fingerprint(request)
                    ? new AcceptedInput(ExistingReceipt(replay.Result.Receipt))
                    : new InputConflict(request.OriginalPayload.Id,
                        "The admission idempotency key was reused with different evidence.");
            }

            if (record.AdmissionsByInput.TryGetValue(request.OriginalPayload.Id, out var stored))
            {
                if (!EquivalentAdmission(
                        stored, request.Context, request.OriginalPayload, request.Preprocessing.OriginalFingerprint))
                {
                    return new InputConflict(request.OriginalPayload.Id,
                        "The input identity was already admitted with different immutable evidence.");
                }

                Persist(JsonSessionStoreLogRecord.ForAdmit(request), cancellationToken);
                var acceptedReplay = new AcceptedInput(ExistingReceipt(stored.Receipt));
                record.AdmissionIdempotency.Add(request.IdempotencyKey,
                    new IdempotencyReceipt<SessionInputAdmissionRequest, AcceptedInput>(request, acceptedReplay));
                return acceptedReplay;
            }

            if (record.Version != request.ExpectedVersion.Value)
            {
                return new RejectedInput(
                    new InputRejection(InputRejectionKind.StaleVersion, "The expected session version is stale."));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!record.Lanes.TryGetValue(laneId, out var lane))
            {
                return new RejectedInput(
                    new InputRejection(InputRejectionKind.AddressNotFound, "The execution lane is not provisioned."));
            }
            if (lane.Revision != request.ExpectedLaneRevision || lane.BranchCursor != request.BranchCursor)
            {
                return new RejectedInput(new InputRejection(
                    InputRejectionKind.StaleVersion, "The expected lane revision or branch cursor is stale."));
            }
            if (record.AdmissionsById.ContainsKey(request.AdmissionId))
            {
                return new InputConflict(request.OriginalPayload.Id, "The admission identity is already reserved.");
            }
            if (record.EntryIds.Contains(request.EntryId))
            {
                return new InputConflict(request.OriginalPayload.Id, "The admission entry identity is already reserved.");
            }

            var pendingCount = record.AdmissionsById.Values.Count(
                static admission => admission.Input.PromotedSequence is null);
            if (pendingCount >= request.MaximumPendingInputs)
            {
                return new QueueCapacityExceeded(
                    new InputCapacityLimit(request.MaximumPendingInputs, pendingCount), retryAfter: null);
            }

            var branch = record.Branches[lane.BranchCursor.BranchId];
            var sequence = new SessionSequence(branch.Entries.Count + 1);
            var admitted = new AdmittedInput(
                request.AdmissionId, request.Context.AgentId, request.Context.SessionId, laneId,
                request.Context.Identity, sequence, request.OriginalPayload, request.EffectivePayload,
                request.Preprocessing, request.AdmittedAt);
            SessionEntryId? parent = branch.Entries.Count == 0 ? null : branch.Entries[^1].Id;
            var entry = new InputAdmittedSessionEntry(
                request.EntryId, request.Context.ToAddress(), (BeforeRunOperationCorrelation) request.Context.Correlation,
                lane.BranchCursor.BranchId, sequence, parent, request.AdmittedAt, new SchemaVersion("1"), admitted);
            var receipt = new AdmissionReceipt(
                admitted.AdmissionId, admitted.OriginalPayload.Id, admitted.AgentId, admitted.SessionId,
                admitted.ExecutionLaneId, admitted.AdmittedSequence, existing: false);
            var retained = new StoredAdmission(
                admitted, (BeforeRunOperationCorrelation) request.Context.Correlation, request.EntryId, receipt);
            var accepted = new AcceptedInput(receipt);

            Persist(JsonSessionStoreLogRecord.ForAdmit(request), cancellationToken);
            record.AdmissionsByInput.Add(admitted.OriginalPayload.Id, retained);
            record.AdmissionsById.Add(admitted.AdmissionId, retained);
            record.AdmissionIdempotency.Add(request.IdempotencyKey,
                new IdempotencyReceipt<SessionInputAdmissionRequest, AcceptedInput>(request, accepted));
            _ = record.EntryIds.Add(entry.Id);
            branch.Entries.Add(entry);
            lane.BranchCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, entry.Id);
            lane.Revision = new SessionLaneRevision(lane.Revision.Value + 1);
            record.Version++;
            record.UpdatedAt = request.AdmittedAt;
            return accepted;
        }
    }

    private SessionRunStartResult AcceptRunCore(SessionRunStartRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the run-start request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return new SessionRunStartRejected("The session is unavailable.");
            }

            if (record.RunStartIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return EquivalentStart(replay.Request, request)
                    ? new SessionRunAccepted(replay.Result.State, replay.Result.SessionVersion, existing: true)
                    : new SessionRunStartConflict(SessionRunStartConflictKind.Idempotency,
                        "The start idempotency key was reused with different evidence.");
            }

            if (request.ExpectedFencingToken is not null)
            {
                return new SessionRunStartFenced(
                    "The JSON store provides host-local coordination and does not accept distributed fences.");
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!record.Lanes.TryGetValue(laneId, out var lane))
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.LaneRevision, "The selected lane does not exist.");
            }
            if (lane.AcceptedState is { } active)
            {
                return new SessionRunStartBusy(active.Correlation.OperationId, active.Correlation.RunId);
            }
            if (lane.Revision != request.ExpectedLaneRevision)
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.LaneRevision, "The selected lane revision is stale.");
            }

            var branch = record.Branches[lane.BranchCursor.BranchId];
            if (lane.BranchCursor != request.BranchCursor
                || BranchCursor(lane.BranchCursor.BranchId, branch) != request.BranchCursor)
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.BranchCursor, "The selected branch cursor is stale.");
            }
            if (record.Version != request.ExpectedVersion.Value)
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.SessionVersion, "The expected session version is stale.");
            }
            if (request.SelectedAdmissionIds.Any(admissionId =>
                    record.AdmissionsById.TryGetValue(admissionId, out var admission)
                    && admission.Input.Identity != request.Context.Identity))
            {
                return new SessionRunStartConflict(SessionRunStartConflictKind.AdmissionIdentity,
                    "Every promoted admission must retain the exact authorized run identity.");
            }
            if (!TrySelectAdmissions(record, request, out var selected))
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.PromotionPlan, "The exact promotion plan is no longer eligible.");
            }

            var initiating = selected.FirstOrDefault(
                stored => stored.Input.AdmissionId == request.InitiatingAdmissionId);
            if (initiating is null || initiating.Correlation != request.Context.Correlation)
            {
                return new SessionRunStartConflict(SessionRunStartConflictKind.AdmissionCorrelation,
                    "The initiating admission correlation differs from the proposed run.");
            }

            var reservedEntryIds = request.EntryIds.Insert(0, request.PromotionEntryId).Add(request.AcceptedEntryId);
            if (reservedEntryIds.Any(record.EntryIds.Contains))
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.PromotionPlan, "A reserved session-entry identity is already in use.");
            }
            if (request.MessageIds.Any(record.MessageIds.Contains))
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.PromotionPlan, "A reserved message identity is already in use.");
            }

            var correlation = new InRunOperationCorrelation(
                request.Context.Correlation.OperationId, request.RunId, request.InitialTurnId);
            var promotionSequence = new SessionSequence(branch.Entries.Count + 1);
            SessionEntryId? priorTip = branch.Entries.Count == 0 ? null : branch.Entries[^1].Id;
            var promotionEntry = new InputPromotedSessionEntry(
                request.PromotionEntryId, request.Context.ToAddress(), correlation, lane.BranchCursor.BranchId,
                promotionSequence, priorTip, request.AcceptedAt, new SchemaVersion("1"), laneId,
                request.InitiatingAdmissionId, request.PromotionCutoff, request.SelectedAdmissionIds);
            var appended = new List<SessionEntry>(selected.Length + 2) { promotionEntry };
            var parent = promotionEntry.Id;
            for (var index = 0; index < selected.Length; index++)
            {
                var stored = selected[index];
                var entrySequence = new SessionSequence(branch.Entries.Count + index + 2);
                var message = new UserMessage(
                    request.MessageIds[index], request.Context.AgentId, request.Context.SessionId,
                    record.ConversationId, lane.BranchCursor.BranchId, request.RunId, request.InitialTurnId,
                    request.AcceptedAt, MessageState.Complete, stored.Input.EffectivePayload.Parts,
                    stored.Input.EffectivePayload.Extensions);
                var entry = new MessageSessionEntry(
                    request.EntryIds[index], request.Context.ToAddress(), correlation, lane.BranchCursor.BranchId,
                    entrySequence, parent, request.AcceptedAt, new SchemaVersion("1"), message);
                appended.Add(entry);
                parent = entry.Id;
            }

            var acceptedSequence = new SessionSequence(branch.Entries.Count + selected.Length + 2);
            var committedCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, request.AcceptedEntryId);
            var installedLaneRevision = new SessionLaneRevision(lane.Revision.Value + 1);
            var state = new SessionAcceptedRunState(
                request.Context.ToAddress(), laneId, installedLaneRevision, correlation,
                request.OperationStateRevision, request.Context.Identity, request.InRunAuthorization,
                request.SessionProfile, request.Configuration, request.BranchCursor, committedCursor,
                request.PromotionCutoff, request.InitiatingAdmissionId, request.SelectedAdmissionIds, request.EntryIds,
                request.MessageIds, request.InitialTurnId, request.AcceptedAt);
            appended.Add(new OperationAcceptedSessionEntry(
                request.AcceptedEntryId, request.Context.ToAddress(), correlation, lane.BranchCursor.BranchId,
                acceptedSequence, parent, request.AcceptedAt, new SchemaVersion("1"), state));

            Persist(JsonSessionStoreLogRecord.ForRunAccepted(request), cancellationToken);
            branch.Entries.AddRange(appended);
            record.EntryIds.UnionWith(reservedEntryIds);
            record.MessageIds.UnionWith(request.MessageIds);
            foreach (var stored in selected)
            {
                stored.Input = new AdmittedInput(
                    stored.Input.AdmissionId, stored.Input.AgentId, stored.Input.SessionId,
                    stored.Input.ExecutionLaneId, stored.Input.Identity, stored.Input.AdmittedSequence,
                    stored.Input.OriginalPayload, stored.Input.EffectivePayload, stored.Input.Preprocessing,
                    stored.Input.AdmittedAt, promotionSequence);
            }

            record.Version++;
            record.UpdatedAt = request.AcceptedAt;
            lane.Revision = installedLaneRevision;
            lane.BranchCursor = committedCursor;
            lane.AcceptedState = state;
            var result = new SessionRunAccepted(state, new SessionVersion(record.Version), existing: false);
            record.RunStartIdempotency.Add(request.IdempotencyKey, new RunStartReceipt(request, result));
            return result;
        }
    }

    private SessionRunStateResult LoadRunStateCore(
        SessionRunStateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the run-state request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            return !TryGetAuthorizedRecord(request.Context, out var record)
                || !record.Lanes.TryGetValue(request.Context.ExecutionLaneId!.Value, out var lane)
                || lane.AcceptedState is not { } state
                || state.Correlation != request.Context.Correlation
                || state.Identity != request.Context.Identity
                ? new SessionRunStateUnavailable("The requested operation state is unavailable.")
                : new SessionRunStateLoaded(state);
        }
    }

    private SessionLaneStateResult LoadLaneStateCore(
        SessionLaneStateRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the lane-state request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            var laneId = request.Context.ExecutionLaneId!.Value;
            return !TryGetAuthorizedRecord(request.Context, out var record)
                ? new SessionLaneStateUnavailable("The session is unavailable.")
                : !record.Lanes.TryGetValue(laneId, out var lane)
                    ? new SessionLaneStateNotProvisioned("The execution lane has not been provisioned.")
                    : new SessionLaneStateLoaded(new SessionLaneState(laneId, lane.Revision, lane.BranchCursor, lane.AcceptedState));
        }
    }

    /// <summary>Atomically clears one lane's installed accepted run, or reconciles a repeated identical release.</summary>
    /// <param name="request">The exact protected release request naming the lane and the accepted run it owns.</param>
    /// <param name="committedAt">The clock-derived instant stamped on the session's last-updated time.</param>
    /// <param name="cancellationToken">Cancels before the durable append that precedes the mutation.</param>
    /// <returns>The released receipt or a typed rejection.</returns>
    /// <remarks>
    /// A missing session, missing lane, and cross-tenant caller are all masked as
    /// <see cref="SessionRunReleaseRejectionKind.LaneNotFound"/> so an unauthorized caller cannot distinguish absence from
    /// denial. A lane whose installed accepted run does not match this request's exact operation, run, and state revision is
    /// <see cref="SessionRunReleaseRejectionKind.Fenced"/>: a stale caller can never clear a different, newer occupant.
    /// </remarks>
    private SessionRunReleaseResult ReleaseRunCore(
        SessionRunReleaseRequest request, DateTimeOffset committedAt, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the release request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return new SessionRunReleaseRejected(
                    SessionRunReleaseRejectionKind.LaneNotFound, "The session is unavailable.");
            }

            if (record.RunReleaseIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return replay.Request.Equals(request)
                    ? new SessionRunReleased(replay.Result.NewVersion, existing: true)
                    : new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Idempotency,
                        "The release idempotency key was reused with different evidence.");
            }
            if (!record.Lanes.TryGetValue(request.ExecutionLaneId, out var lane))
            {
                return new SessionRunReleaseRejected(
                    SessionRunReleaseRejectionKind.LaneNotFound, "The selected lane does not exist.");
            }
            if (lane.AcceptedState is not { } active)
            {
                return new SessionRunReleaseRejected(
                    SessionRunReleaseRejectionKind.NoAcceptedRun, "The selected lane holds no accepted run.");
            }
            if (active.Correlation.OperationId != request.OperationId
                || active.Correlation.RunId != request.RunId
                || active.OperationStateRevision != request.ExpectedStateRevision)
            {
                return new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Fenced,
                    "The lane's installed accepted run does not match the requested operation, run, and state revision.");
            }
            if (record.Version != request.ExpectedVersion.Value)
            {
                return new SessionRunReleaseRejected(
                    SessionRunReleaseRejectionKind.SessionVersion, "The expected session version is stale.");
            }

            Persist(JsonSessionStoreLogRecord.ForRunReleased(request, committedAt), cancellationToken);
            lane.AcceptedState = null;
            record.Version++;
            record.UpdatedAt = committedAt;
            var released = new SessionRunReleased(new SessionVersion(record.Version), existing: false);
            record.RunReleaseIdempotency[request.IdempotencyKey] =
                new IdempotencyReceipt<SessionRunReleaseRequest, SessionRunReleased>(request, released);
            return released;
        }
    }

    private SessionPendingInputsResult LoadPendingInputsCore(
        SessionPendingInputsRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the pending-input discovery request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return new SessionPendingInputsUnavailable("The session is unavailable.");
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!record.Lanes.TryGetValue(laneId, out var lane))
            {
                return new SessionPendingInputsUnavailable("The execution lane has not been provisioned.");
            }

            var pending = record.AdmissionsById.Values
                .Where(stored => stored.Input.ExecutionLaneId == laneId && stored.Input.PromotedSequence is null)
                .Select(static stored => stored.Input)
                .OrderBy(static input => input.AdmittedSequence.Value)
                .ToImmutableArray();
            return new SessionPendingInputsLoaded(
                request.Context.AgentId, request.Context.SessionId, laneId, pending, lane.Revision, lane.BranchCursor);
        }
    }

    /// <summary>Atomically promotes a durably admitted selection into an already-accepted run's current turn, or reconciles a repeated identical promotion.</summary>
    /// <param name="request">The exact protected mid-run promotion request.</param>
    /// <param name="cancellationToken">Cancels before the durable append that precedes the mutation.</param>
    /// <returns>The committed promotion or a typed rejection.</returns>
    /// <remarks>
    /// The accepted run's <see cref="SessionAcceptedRunState.PromotedAdmissionIds"/>,
    /// <see cref="SessionAcceptedRunState.MaterializedEntryIds"/>, and <see cref="SessionAcceptedRunState.MaterializedMessageIds"/>
    /// continue to describe only the run's initial acceptance; a mid-run promotion advances the lane's committed
    /// cursor and <see cref="SessionAcceptedRunState.OperationStateRevision"/> without rewriting that initial record.
    /// </remarks>
    private SessionInputPromotionResult PromoteInputCore(
        SessionInputPromotionRequest request, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public boundary validates the promotion request.");
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return new SessionInputPromotionRejected("The session is unavailable.");
            }

            if (record.InputPromotionIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return EquivalentPromotion(replay.Request, request)
                    ? replay.Result
                    : new SessionInputPromotionRejected("The promotion idempotency key was reused with different evidence.");
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!record.Lanes.TryGetValue(laneId, out var lane))
            {
                return new SessionInputPromotionRejected("The selected lane does not exist.");
            }

            if (lane.AcceptedState is not { } active)
            {
                return new SessionInputPromotionRejected("The selected lane holds no accepted run.");
            }

            if (active.Correlation != request.Context.Correlation || active.OperationStateRevision != request.ExpectedStateRevision)
            {
                return new SessionInputPromotionRejected(
                    "The lane's installed accepted run does not match the requested operation, run, turn, and state revision.");
            }

            if (lane.Revision != request.ExpectedLaneRevision)
            {
                return new SessionInputPromotionRejected("The selected lane revision is stale.");
            }

            var branch = record.Branches[lane.BranchCursor.BranchId];
            if (lane.BranchCursor != request.BranchCursor
                || BranchCursor(lane.BranchCursor.BranchId, branch) != request.BranchCursor)
            {
                return new SessionInputPromotionRejected("The selected branch cursor is stale.");
            }

            if (record.Version != request.ExpectedVersion.Value)
            {
                return new SessionInputPromotionRejected("The expected session version is stale.");
            }

            if (!TrySelectPendingAdmissions(record, request, out var selected))
            {
                return new SessionInputPromotionRejected("The exact promotion selection is no longer eligible.");
            }

            var reservedEntryIds = request.EntryIds.Insert(0, request.PromotionEntryId);
            if (reservedEntryIds.Any(record.EntryIds.Contains))
            {
                return new SessionInputPromotionRejected("A reserved session-entry identity is already in use.");
            }

            if (request.MessageIds.Any(record.MessageIds.Contains))
            {
                return new SessionInputPromotionRejected("A reserved message identity is already in use.");
            }

            var correlation = (InRunOperationCorrelation) request.Context.Correlation;
            var promotionSequence = new SessionSequence(branch.Entries.Count + 1);
            SessionEntryId? priorTip = branch.Entries.Count == 0 ? null : branch.Entries[^1].Id;
            var promotionEntry = new InputPromotedSessionEntry(
                request.PromotionEntryId, request.Context.ToAddress(), correlation, lane.BranchCursor.BranchId,
                promotionSequence, priorTip, request.PromotedAt, new SchemaVersion("1"), laneId,
                request.SelectedAdmissionIds[0], request.CutoffSequence, request.SelectedAdmissionIds);
            var appended = new List<SessionEntry>(selected.Length + 1) { promotionEntry };
            var parent = promotionEntry.Id;
            for (var index = 0; index < selected.Length; index++)
            {
                var stored = selected[index];
                var entrySequence = new SessionSequence(branch.Entries.Count + index + 2);
                var message = new UserMessage(
                    request.MessageIds[index], request.Context.AgentId, request.Context.SessionId,
                    record.ConversationId, lane.BranchCursor.BranchId, correlation.RunId, request.TargetTurnId,
                    request.PromotedAt, MessageState.Complete, stored.Input.EffectivePayload.Parts,
                    stored.Input.EffectivePayload.Extensions);
                var entry = new MessageSessionEntry(
                    request.EntryIds[index], request.Context.ToAddress(), correlation, lane.BranchCursor.BranchId,
                    entrySequence, parent, request.PromotedAt, new SchemaVersion("1"), message);
                appended.Add(entry);
                parent = entry.Id;
            }

            Persist(JsonSessionStoreLogRecord.ForInputPromoted(request), cancellationToken);
            var committedCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, parent);
            var installedLaneRevision = new SessionLaneRevision(lane.Revision.Value + 1);
            var installedStateRevision = new OperationStateRevision(active.OperationStateRevision.Value + 1);
            branch.Entries.AddRange(appended);
            record.EntryIds.UnionWith(reservedEntryIds);
            record.MessageIds.UnionWith(request.MessageIds);
            var promoted = ImmutableArray.CreateBuilder<AdmittedInput>(selected.Length);
            foreach (var stored in selected)
            {
                stored.Input = new AdmittedInput(
                    stored.Input.AdmissionId, stored.Input.AgentId, stored.Input.SessionId,
                    stored.Input.ExecutionLaneId, stored.Input.Identity, stored.Input.AdmittedSequence,
                    stored.Input.OriginalPayload, stored.Input.EffectivePayload, stored.Input.Preprocessing,
                    stored.Input.AdmittedAt, promotionSequence);
                promoted.Add(stored.Input);
            }

            record.Version++;
            record.UpdatedAt = request.PromotedAt;
            lane.Revision = installedLaneRevision;
            lane.BranchCursor = committedCursor;
            lane.AcceptedState = new SessionAcceptedRunState(
                active.Address, active.ExecutionLaneId, installedLaneRevision, active.Correlation,
                installedStateRevision, active.Identity, active.Authorization, active.SessionProfile,
                active.Configuration, active.PreviousCursor, committedCursor, active.PromotionCutoff,
                active.InitiatingAdmissionId, active.PromotedAdmissionIds, active.MaterializedEntryIds,
                active.MaterializedMessageIds, active.InitialTurnId, active.AcceptedAt);
            var result = new SessionInputPromoted(
                promoted.MoveToImmutable(), committedCursor, new SessionVersion(record.Version), installedStateRevision);
            record.InputPromotionIdempotency.Add(request.IdempotencyKey, new InputPromotionReceipt(request, result));
            return result;
        }
    }

    private static bool TrySelectPendingAdmissions(SessionRecord record, SessionInputPromotionRequest request,
        out ImmutableArray<StoredAdmission> selected)
    {
        Debug.Assert(record is not null, "A loaded session is required.");
        Debug.Assert(request is not null, "A validated promotion request is required.");
        var builder = ImmutableArray.CreateBuilder<StoredAdmission>(request.SelectedAdmissionIds.Length);
        foreach (var admissionId in request.SelectedAdmissionIds)
        {
            if (!record.AdmissionsById.TryGetValue(admissionId, out var stored)
                || stored.Input.PromotedSequence is not null
                || stored.Input.ExecutionLaneId != request.Context.ExecutionLaneId
                || stored.Input.Identity != request.Context.Identity
                || stored.Input.AdmittedSequence.Value > request.CutoffSequence.Value)
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

    private static bool EquivalentPromotion(SessionInputPromotionRequest left, SessionInputPromotionRequest right)
    {
        Debug.Assert(left is not null && right is not null, "Promotion requests are required.");
        return left.Context == right.Context
            && left.SelectedAdmissionIds.SequenceEqual(right.SelectedAdmissionIds)
            && left.CutoffSequence == right.CutoffSequence
            && left.ExpectedLaneRevision == right.ExpectedLaneRevision
            && left.ExpectedVersion == right.ExpectedVersion
            && left.BranchCursor == right.BranchCursor
            && left.PromotionEntryId == right.PromotionEntryId
            && left.EntryIds.SequenceEqual(right.EntryIds)
            && left.MessageIds.SequenceEqual(right.MessageIds)
            && left.ExpectedStateRevision == right.ExpectedStateRevision
            && left.PromotedAt == right.PromotedAt;
    }

    /// <summary>Retains bounded store-issued provenance for exact continuation snapshots.</summary>
    /// <param name="snapshot">The snapshot created under the serialized read boundary.</param>
    /// <remarks>Retention is deliberately process-local: continuation provenance is not durable state and is not logged.</remarks>
    private void RetainIssuedReadSnapshot(SessionReadSnapshot snapshot)
    {
        Debug.Assert(snapshot is not null, "The read path creates a nonnull snapshot before retention.");
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

    /// <summary>Moves a successful creation receipt into the deleted index so a later retry reports deletion.</summary>
    /// <param name="address">The address of the session that was just removed.</param>
    private void RetireCreateReceipt(SessionAddress address)
    {
        Debug.Assert(address is not null, "The delete path resolves a complete address before retirement.");
        var createReceipt = _createIdempotency.FirstOrDefault(
            pair => pair.Value.Result.Descriptor.Address == address);
        if (createReceipt.Value is null)
        {
            return;
        }

        _ = _createIdempotency.Remove(createReceipt.Key);
        _deletedCreateIdempotency[createReceipt.Key] = createReceipt.Value.Request;
    }

    private bool TryGetAuthorizedRecord(SessionOperationContext context, [NotNullWhen(true)] out SessionRecord? record)
    {
        Debug.Assert(context is not null, "A validated session operation context is required.");
        return _sessions.TryGetValue(context.ToAddress(), out record)
            && record.TenantId == context.Identity.TenantId;
    }

    private SessionDescriptor ToDescriptor(SessionRecord record)
    {
        Debug.Assert(record is not null, "A projected session record is required.");
        return new SessionDescriptor(
            record.Address, record.ConversationId, record.TenantId, record.OwnerId, Descriptor.Key,
            record.ActiveBranchId, new SessionVersion(record.Version), record.State, record.CreatedAt,
            record.UpdatedAt, new SchemaVersion("1"), ExtensionData.Empty);
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
    /// <param name="record">The session record whose branch was just extended.</param>
    /// <param name="request">The committed append whose context names the appending lane.</param>
    /// <remarks>
    /// Only the lane named by <see cref="SessionOperationContext.ExecutionLaneId"/> advances, and only when its cursor is
    /// bound to <see cref="SessionAppendRequest.BranchId"/>. Session-wide appends without a lane and appends to a branch
    /// owned by a different lane leave every lane cursor untouched. The lane revision is unchanged; later admission or
    /// acceptance still validates the cursor against the real branch tip.
    /// </remarks>
    private static void AdvanceOwningLaneCursor(SessionRecord record, SessionAppendRequest request)
    {
        Debug.Assert(record is not null, "A projected session is required.");
        Debug.Assert(request is not null, "A committed append request is required.");
        Debug.Assert(!request.Entries.IsEmpty, "Append validation rejects empty batches before commit.");
        if (request.Context.ExecutionLaneId is { } laneId
            && record.Lanes.TryGetValue(laneId, out var lane)
            && lane.BranchCursor.BranchId == request.BranchId)
        {
            lane.BranchCursor = new SessionBranchCursor(request.BranchId, request.Entries[^1].Id);
        }
    }

    /// <summary>Determines whether a fork point names the empty prefix or an entry actually committed on the parent branch.</summary>
    /// <param name="parent">The projected parent branch.</param>
    /// <param name="atSequence">The requested fork sequence.</param>
    /// <returns><see langword="true"/> when <paramref name="atSequence"/> is zero or equals a committed parent-branch sequence.</returns>
    /// <remarks>
    /// Sequences are per-branch coordinates, so an identical numeric sequence on a sibling branch names a different entry,
    /// if any. This check accepts only a sequence actually present in <paramref name="parent"/>'s own entry list.
    /// </remarks>
    private static bool IsCommittedForkPoint(BranchRecord parent, SessionSequence atSequence)
    {
        Debug.Assert(parent is not null, "A projected parent branch is required.");
        return atSequence.Value == 0 || parent.Entries.Any(entry => entry.Sequence.Value == atSequence.Value);
    }

    private static SessionBranchCursor BranchCursor(BranchId branchId, BranchRecord branch)
    {
        Debug.Assert(branch is not null, "A projected branch is required.");
        return new SessionBranchCursor(branchId, branch.Entries.Count == 0 ? null : branch.Entries[^1].Id);
    }

    private static bool TrySelectAdmissions(SessionRecord record, SessionRunStartRequest request,
        out ImmutableArray<StoredAdmission> selected)
    {
        Debug.Assert(record is not null, "A projected session is required.");
        Debug.Assert(request is not null, "A validated start request is required.");
        var builder = ImmutableArray.CreateBuilder<StoredAdmission>(request.SelectedAdmissionIds.Length);
        foreach (var admissionId in request.SelectedAdmissionIds)
        {
            if (!record.AdmissionsById.TryGetValue(admissionId, out var stored)
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
}
