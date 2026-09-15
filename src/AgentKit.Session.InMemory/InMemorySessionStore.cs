// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>
/// A deterministic, process-local, non-durable <see cref="ISessionStore"/>
/// suitable for tests, examples, and short-lived applications.
/// </summary>
/// <remarks>
/// <para>
/// Every operation is serialized by one store-wide lock. This sacrifices
/// cross-session concurrency in exchange for trivially correct
/// linearizable behavior, which is the right trade-off for a store whose
/// entire purpose is deterministic testing rather than production
/// throughput. All state is lost when the process exits;
/// <see cref="Descriptor"/> reports <c>Durable: false</c> accordingly.
/// </para>
/// <para>
/// Each <see cref="SessionEntry.Sequence"/> is the entry's 1-based position
/// within its own branch: it is simply that branch's entry-list count at
/// commit time. <see cref="SessionVersion"/> remains the canonical
/// whole-session optimistic-concurrency token and advances once per
/// committed mutation regardless of which branch it targets. Branching
/// copies the referenced entries (by value; the copied
/// <see cref="SessionEntry"/> instances keep their original identity and
/// branch provenance) into the new branch's own list, so appends to either
/// branch afterward never affect the other and each branch's sequence
/// coordinates are independent of its siblings'. A <see cref="SessionEntryId"/>,
/// not the pair of branch and sequence, is the cross-branch identity of an
/// entry.
/// </para>
/// <para>
/// Exact paged-read continuations require a snapshot previously issued by this
/// store instance. The adapter retains a bounded number of distinct issued
/// snapshots in first-in, first-out order, configured through
/// <see cref="InMemorySessionStoreOptions.MaximumIssuedReadSnapshots"/> and
/// defaulting to 4,096; an evicted value fails as unavailable instead of being
/// trusted as caller-authored evidence.
/// </para>
/// </remarks>
public sealed partial class InMemorySessionStore: ISessionStore
{
    /// <summary>Bounds process-local continuation evidence retained by this ephemeral adapter.</summary>
    private readonly int _maximumIssuedReadSnapshots;
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
    private readonly ILogger<InMemorySessionStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionStore"/> class
    /// with the default <see cref="InMemorySessionStoreOptions"/> bounds.
    /// </summary>
    /// <param name="branchIds">Generates the identity of each newly created branch.</param>
    /// <param name="auditRecordIds">Generates stable identities for required audit intents.</param>
    /// <param name="auditDispatcher">The required audit dispatcher that must accept each consumed access intent.</param>
    /// <param name="grants">The authoritative store that validates and consumes exact session-access grants.</param>
    /// <param name="timeProvider">The clock used to timestamp created and updated sessions.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <remarks>
    /// Equivalent to the options-accepting constructor supplied with a fresh
    /// <see cref="InMemorySessionStoreOptions"/>, so the store retains up to
    /// 4,096 issued read snapshots.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any required parameter is null.</exception>
    public InMemorySessionStore(
        IIdentifierGenerator<BranchId> branchIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ILogger<InMemorySessionStore>? logger = null)
        : this(Options.Create(new InMemorySessionStoreOptions()), branchIds, auditRecordIds, auditDispatcher, grants, timeProvider, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySessionStore"/> class
    /// with explicit process-local bounds.
    /// </summary>
    /// <param name="options">The bound configuration; its value is read once during construction and never observed again.</param>
    /// <param name="branchIds">Generates the identity of each newly created branch.</param>
    /// <param name="auditRecordIds">Generates stable identities for required audit intents.</param>
    /// <param name="auditDispatcher">The required audit dispatcher that must accept each consumed access intent.</param>
    /// <param name="grants">The authoritative store that validates and consumes exact session-access grants.</param>
    /// <param name="timeProvider">The clock used to timestamp created and updated sessions.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <remarks>
    /// The bounds are validated here in addition to any options-pipeline
    /// validation so that a directly constructed store cannot start with an
    /// unenforceable limit.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/>, its <see cref="IOptions{TOptions}.Value"/>, or any
    /// other required parameter is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="InMemorySessionStoreOptions.MaximumIssuedReadSnapshots"/> is zero or negative.
    /// </exception>
    public InMemorySessionStore(
        IOptions<InMemorySessionStoreOptions> options,
        IIdentifierGenerator<BranchId> branchIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ILogger<InMemorySessionStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumIssuedReadSnapshots, nameof(options));
        ArgumentNullException.ThrowIfNull(branchIds);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _maximumIssuedReadSnapshots = options.Value.MaximumIssuedReadSnapshots;
        _branchIds = branchIds;
        _auditRecordIds = auditRecordIds;
        _auditDispatcher = auditDispatcher;
        _grants = grants;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<InMemorySessionStore>.Instance;
    }

    /// <inheritdoc/>
    public SessionStoreDescriptor Descriptor { get; } = new(
        new SessionStoreKey("agentkit.in-memory"),
        SessionStoreCapabilities.Branching | SessionStoreCapabilities.Transactions,
        SessionConsistencyModel.Strong,
        durable: false,
        supportsDistributedFencing: false);

    /// <inheritdoc/>
    private ValueTask<SessionCreateResult> CreateCoreAsync(
        SessionStoreCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var logical = request.Request;
            var idempotencyEntry = (logical.Identity.TenantId, logical.AgentId, logical.IdempotencyKey);
            if (_deletedCreateIdempotency.TryGetValue(idempotencyEntry, out var deletedRequest))
            {
                return ValueTask.FromResult<SessionCreateResult>(
                    deletedRequest.Equals(request)
                        ? new SessionCreateFailed("The session created by this idempotency key was deleted.")
                        : new SessionCreateFailed("The idempotency key was previously used with different request evidence."));
            }

            if (_createIdempotency.TryGetValue(idempotencyEntry, out var existingReceipt))
            {
                return ValueTask.FromResult<SessionCreateResult>(
                    existingReceipt.Request.Equals(request)
                        ? existingReceipt.Result
                        : new SessionCreateFailed("The idempotency key was previously used with different request evidence."));
            }

            if (_sessions.ContainsKey(request.Address))
            {
                return ValueTask.FromResult<SessionCreateResult>(new SessionCreateFailed(
                    "The allocated session address is already present."));
            }

            var now = _timeProvider.GetUtcNow();
            var branchId = _branchIds.Create();
            var address = request.Address;
            var record = new SessionRecord(
                address,
                logical.ConversationId,
                logical.Identity.TenantId,
                logical.Identity.PrincipalId,
                branchId,
                now);
            record.Branches[branchId] = new BranchRecord();

            _sessions[address] = record;
            var created = new SessionCreated(ToDescriptor(record), existing: false);
            _createIdempotency[idempotencyEntry] = new IdempotencyReceipt<SessionStoreCreateRequest, SessionCreated>(request, created);

            return ValueTask.FromResult<SessionCreateResult>(created);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionLoadResult> LoadCoreAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = context.ToAddress();
            return !_sessions.TryGetValue(address, out var record)
                ? ValueTask.FromResult<SessionLoadResult>(new SessionNotFound(address))
                : record.TenantId != context.Identity.TenantId
                    ? ValueTask.FromResult<SessionLoadResult>(new SessionNotFound(address))
                    : ValueTask.FromResult<SessionLoadResult>(new SessionLoaded(ToDescriptor(record)));
        }
    }

    private ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneCoreAsync(
        SessionExecutionLaneProvisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionRejected("The session is unavailable."));
            }

            if (record.LaneProvisionIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(replay.Request == request
                    ? new SessionExecutionLaneProvisioned(replay.Result.ExecutionLaneId, replay.Result.BranchCursor,
                        replay.Result.LaneRevision, replay.Result.SessionVersion, existing: true)
                    : new SessionExecutionLaneProvisionConflict(
                        "The provisioning idempotency key was reused with different evidence."));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (record.Lanes.ContainsKey(laneId))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionConflict("The execution lane is already provisioned."));
            }

            if (record.Version != request.ExpectedVersion.Value)
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionConflict("The expected session version is stale."));
            }

            if (!record.Branches.TryGetValue(request.BranchCursor.BranchId, out var branch)
                || BranchCursor(request.BranchCursor.BranchId, branch) != request.BranchCursor)
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionConflict("The branch cursor is stale or unavailable."));
            }

            if (record.Lanes.Values.Any(lane => lane.BranchCursor.BranchId == request.BranchCursor.BranchId))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionConflict("The branch is already owned by another execution lane."));
            }

            if (record.EntryIds.Contains(request.EntryId))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionConflict("The provisioning entry identity is already reserved."));
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

            branch.Entries.Add(entry);
            _ = record.EntryIds.Add(entry.Id);
            record.Lanes.Add(laneId, new LaneRecord(committedCursor, laneRevision));
            record.LaneProvisionIdempotency.Add(request.IdempotencyKey,
                new IdempotencyReceipt<SessionExecutionLaneProvisionRequest, SessionExecutionLaneProvisioned>(request, result));
            record.Version = sessionVersion.Value;
            record.UpdatedAt = request.ProvisionedAt;
            return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(result);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionAppendResult> AppendCoreAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record))
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(address));
            }

            if (record.TenantId != request.Context.Identity.TenantId)
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(address));
            }

            if (!record.Branches.TryGetValue(request.BranchId, out var branch))
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendNotFound(address));
            }

            if (branch.AppendIdempotency.TryGetValue(request.IdempotencyKey, out var cached))
            {
                return ValueTask.FromResult<SessionAppendResult>(
                    cached.Request.Equals(request)
                        ? cached.Result
                        : new SessionAppendFailed("The idempotency key was previously used with different request evidence."));
            }

            var currentVersion = new SessionVersion(record.Version);
            if (currentVersion.Value != request.ExpectedVersion.Value)
            {
                return ValueTask.FromResult<SessionAppendResult>(
                    new SessionAppendConflict(request.ExpectedVersion, currentVersion));
            }

            for (var i = 0; i < request.Entries.Length; i++)
            {
                var expectedSequence = branch.Entries.Count + i + 1;
                if (request.Entries[i].Sequence.Value != expectedSequence)
                {
                    return ValueTask.FromResult<SessionAppendResult>(new SessionAppendFailed(
                        $"Entry at position {i} has sequence {request.Entries[i].Sequence.Value}; expected {expectedSequence}."));
                }
            }

            var proposedEntryIds = request.Entries.Select(static entry => entry.Id).ToArray();
            if (proposedEntryIds.Distinct().Count() != proposedEntryIds.Length
                || proposedEntryIds.Any(record.EntryIds.Contains))
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendFailed(
                    "An appended entry identity is already reserved."));
            }

            var proposedMessageIds = request.Entries
                .OfType<MessageSessionEntry>()
                .Select(static entry => entry.Message.Id)
                .ToArray();
            if (proposedMessageIds.Distinct().Count() != proposedMessageIds.Length
                || proposedMessageIds.Any(record.MessageIds.Contains))
            {
                return ValueTask.FromResult<SessionAppendResult>(new SessionAppendFailed(
                    "An appended message identity is already reserved."));
            }

            branch.Entries.AddRange(request.Entries);
            record.EntryIds.UnionWith(proposedEntryIds);
            record.MessageIds.UnionWith(proposedMessageIds);
            record.Version++;
            record.UpdatedAt = _timeProvider.GetUtcNow();
            AdvanceOwningLaneCursor(record, request);

            var newVersion = new SessionVersion(record.Version);
            var appended = new SessionAppended(newVersion, request.Entries);
            branch.AppendIdempotency[request.IdempotencyKey] = new IdempotencyReceipt<SessionAppendRequest, SessionAppended>(request, appended);

            return ValueTask.FromResult<SessionAppendResult>(appended);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionPageResult> ReadCoreAsync(
        SessionReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.BranchId, out var branch))
            {
                return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(address));
            }

            var upperSequence = branch.Entries.Count == 0
                ? new SessionSequence(0)
                : branch.Entries[^1].Sequence;
            if (request.Snapshot is null && request.FromSequenceExclusive.Value > upperSequence.Value)
            {
                return ValueTask.FromResult<SessionPageResult>(new SessionReadFailed(
                    "The requested starting sequence is beyond the current branch tip."));
            }

            if (request.Snapshot is { } supplied
                && (!_issuedReadSnapshots.Contains(supplied)
                    || supplied.Version.Value > record.Version
                    || supplied.UpperSequence.Value > upperSequence.Value))
            {
                return ValueTask.FromResult<SessionPageResult>(new SessionReadFailed(
                    "The supplied session read snapshot is not available for this branch."));
            }

            var snapshot = request.Snapshot ?? new SessionReadSnapshot(
                address,
                request.BranchId,
                new SessionVersion(record.Version),
                upperSequence);
            if (request.Snapshot is null)
            {
                RetainIssuedReadSnapshot(snapshot);
            }

            var pageEntries = branch.Entries
                .Where(entry => entry.Sequence.Value > request.FromSequenceExclusive.Value
                    && entry.Sequence.Value <= snapshot.UpperSequence.Value)
                .Take(request.PageSize)
                .ToImmutableArray();
            var throughSequence = pageEntries.IsEmpty
                ? request.FromSequenceExclusive
                : pageEntries[^1].Sequence;
            var hasMore = branch.Entries.Any(entry => entry.Sequence.Value > throughSequence.Value
                && entry.Sequence.Value <= snapshot.UpperSequence.Value);

            return ValueTask.FromResult<SessionPageResult>(new SessionPage(
                pageEntries,
                throughSequence,
                hasMore,
                snapshot));
        }
    }

    /// <summary>Retains bounded store-issued provenance for exact continuation snapshots.</summary>
    /// <param name="snapshot">The snapshot created under the serialized read boundary.</param>
    private void RetainIssuedReadSnapshot(SessionReadSnapshot snapshot)
    {
        Debug.Assert(snapshot is not null, "The read path creates a nonnull snapshot before retention.");
        if (!_issuedReadSnapshots.Add(snapshot))
        {
            return;
        }

        _issuedReadSnapshotOrder.Enqueue(snapshot);
        if (_issuedReadSnapshotOrder.Count > _maximumIssuedReadSnapshots)
        {
            _ = _issuedReadSnapshots.Remove(_issuedReadSnapshotOrder.Dequeue());
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionBranchResult> CreateBranchCoreAsync(
        SessionBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            if (!_sessions.TryGetValue(address, out var record)
                || record.TenantId != request.Context.Identity.TenantId
                || !record.Branches.TryGetValue(request.ParentBranchId, out var parentBranch))
            {
                return ValueTask.FromResult<SessionBranchResult>(
                    new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence));
            }

            if (record.BranchIdempotency.TryGetValue(request.IdempotencyKey, out var existingReceipt))
            {
                return ValueTask.FromResult<SessionBranchResult>(
                    existingReceipt.Request.Equals(request)
                        ? existingReceipt.Result
                        : new SessionBranchFailed("The idempotency key was previously used with different request evidence."));
            }

            if (!IsCommittedForkPoint(parentBranch, request.AtSequence))
            {
                return ValueTask.FromResult<SessionBranchResult>(
                    new SessionBranchParentNotFound(request.ParentBranchId, request.AtSequence));
            }

            var newBranchId = _branchIds.Create();
            var newBranch = new BranchRecord();
            newBranch.Entries.AddRange(parentBranch.Entries.Where(entry => entry.Sequence.Value <= request.AtSequence.Value));

            record.Branches[newBranchId] = newBranch;
            var branched = new SessionBranched(newBranchId, request.AtSequence);
            record.BranchIdempotency[request.IdempotencyKey] = new IdempotencyReceipt<SessionBranchRequest, SessionBranched>(request, branched);
            record.UpdatedAt = _timeProvider.GetUtcNow();
            record.Version++;

            return ValueTask.FromResult<SessionBranchResult>(branched);
        }
    }

    /// <inheritdoc/>
    private ValueTask<SessionDeleteResult> DeleteCoreAsync(
        SessionDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using (_gate.EnterScope())
        {
            var address = request.Context.ToAddress();
            var idempotencyEntry = (request.Context.Identity.TenantId, address, request.IdempotencyKey);
            if (_deleteIdempotency.TryGetValue(idempotencyEntry, out var existingReceipt))
            {
                return ValueTask.FromResult<SessionDeleteResult>(
                    existingReceipt.Request.Equals(request)
                        ? existingReceipt.Result
                        : new SessionDeleteFailed("The idempotency key was previously used with different request evidence."));
            }

            if (_sessions.TryGetValue(address, out var record)
                && record.TenantId != request.Context.Identity.TenantId)
            {
                return ValueTask.FromResult<SessionDeleteResult>(new SessionDeleted(address));
            }

            if (_sessions.Remove(address))
            {
                var createReceipt = _createIdempotency
                    .FirstOrDefault(pair => pair.Value.Result.Descriptor.Address == address);
                if (createReceipt.Value is not null)
                {
                    _ = _createIdempotency.Remove(createReceipt.Key);
                    _deletedCreateIdempotency[createReceipt.Key] = createReceipt.Value.Request;
                }
            }

            var deleted = new SessionDeleted(address);
            _deleteIdempotency[idempotencyEntry] = new IdempotencyReceipt<SessionDeleteRequest, SessionDeleted>(request, deleted);
            return ValueTask.FromResult<SessionDeleteResult>(deleted);
        }
    }

    private ValueTask<SessionInputLookupResult> LookupInputCoreAsync(
        SessionInputLookupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            return !TryGetAuthorizedRecord(request.Context, out var record)
                ? ValueTask.FromResult<SessionInputLookupResult>(new SessionInputNotFound())
                : !record.AdmissionsByInput.TryGetValue(request.Input.Id, out var stored)
                ? ValueTask.FromResult<SessionInputLookupResult>(new SessionInputNotFound())
                : !EquivalentAdmission(stored, request.Context, request.Input, request.OriginalFingerprint)
                ? ValueTask.FromResult<SessionInputLookupResult>(new SessionInputLookupConflict(
                    request.Input.Id, "The input identity was already admitted with different immutable evidence."))
                : ValueTask.FromResult<SessionInputLookupResult>(new SessionInputReplayFound(
                stored.Input, stored.Correlation, ExistingReceipt(stored.Receipt)));
        }
    }

    private ValueTask<InputAdmissionResult> AdmitInputCoreAsync(
        SessionInputAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.AddressNotFound, "The session is unavailable.")));
            }

            if (record.AdmissionIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return ValueTask.FromResult<InputAdmissionResult>(
                    SessionStoreSecurityBinding.Fingerprint(replay.Request) == SessionStoreSecurityBinding.Fingerprint(request)
                        ? new AcceptedInput(ExistingReceipt(replay.Result.Receipt))
                        : new InputConflict(request.OriginalPayload.Id,
                            "The admission idempotency key was reused with different evidence."));
            }

            if (record.AdmissionsByInput.TryGetValue(request.OriginalPayload.Id, out var stored))
            {
                if (!EquivalentAdmission(stored, request.Context, request.OriginalPayload, request.Preprocessing.OriginalFingerprint))
                {
                    return ValueTask.FromResult<InputAdmissionResult>(new InputConflict(
                        request.OriginalPayload.Id, "The input identity was already admitted with different immutable evidence."));
                }

                var acceptedReplay = new AcceptedInput(ExistingReceipt(stored.Receipt));
                record.AdmissionIdempotency.Add(request.IdempotencyKey,
                    new IdempotencyReceipt<SessionInputAdmissionRequest, AcceptedInput>(request, acceptedReplay));
                return ValueTask.FromResult<InputAdmissionResult>(acceptedReplay);
            }

            if (record.Version != request.ExpectedVersion.Value)
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.StaleVersion, "The expected session version is stale.")));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!record.Lanes.TryGetValue(laneId, out var lane))
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.AddressNotFound, "The execution lane is not provisioned.")));
            }

            if (lane.Revision != request.ExpectedLaneRevision || lane.BranchCursor != request.BranchCursor)
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.StaleVersion, "The expected lane revision or branch cursor is stale.")));
            }

            if (record.AdmissionsById.ContainsKey(request.AdmissionId))
            {
                return ValueTask.FromResult<InputAdmissionResult>(new InputConflict(
                    request.OriginalPayload.Id, "The admission identity is already reserved."));
            }

            if (record.EntryIds.Contains(request.EntryId))
            {
                return ValueTask.FromResult<InputAdmissionResult>(new InputConflict(
                    request.OriginalPayload.Id, "The admission entry identity is already reserved."));
            }

            var pendingCount = record.AdmissionsById.Values.Count(static admission => admission.Input.PromotedSequence is null);
            if (pendingCount >= request.MaximumPendingInputs)
            {
                return ValueTask.FromResult<InputAdmissionResult>(new QueueCapacityExceeded(
                    new InputCapacityLimit(request.MaximumPendingInputs, pendingCount), retryAfter: null));
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
            var retained = new StoredAdmission(admitted, (BeforeRunOperationCorrelation) request.Context.Correlation, request.EntryId, receipt);
            var accepted = new AcceptedInput(receipt);
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
            return ValueTask.FromResult<InputAdmissionResult>(accepted);
        }
    }

    private ValueTask<SessionRunStartResult> AcceptRunCoreAsync(
        SessionRunStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            if (!TryGetAuthorizedRecord(request.Context, out var record))
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartRejected("The session is unavailable."));
            }

            if (record.RunStartIdempotency.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return ValueTask.FromResult<SessionRunStartResult>(EquivalentStart(replay.Request, request)
                    ? new SessionRunAccepted(replay.Result.State, replay.Result.SessionVersion, existing: true)
                    : new SessionRunStartConflict(SessionRunStartConflictKind.Idempotency,
                        "The start idempotency key was reused with different evidence."));
            }

            if (request.ExpectedFencingToken is not null)
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartFenced(
                    "The in-memory store provides process-local coordination and does not accept distributed fences."));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!record.Lanes.TryGetValue(laneId, out var lane))
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.LaneRevision, "The selected lane does not exist."));
            }

            if (lane.AcceptedState is { } active)
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartBusy(
                    active.Correlation.OperationId, active.Correlation.RunId));
            }

            if (lane.Revision != request.ExpectedLaneRevision)
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.LaneRevision, "The selected lane revision is stale."));
            }

            var branch = record.Branches[lane.BranchCursor.BranchId];
            if (lane.BranchCursor != request.BranchCursor
                || BranchCursor(lane.BranchCursor.BranchId, branch) != request.BranchCursor)
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.BranchCursor, "The selected branch cursor is stale."));
            }

            if (record.Version != request.ExpectedVersion.Value)
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.SessionVersion, "The expected session version is stale."));
            }

            if (request.SelectedAdmissionIds.Any(admissionId =>
                    record.AdmissionsById.TryGetValue(admissionId, out var admission)
                    && admission.Input.Identity != request.Context.Identity))
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.AdmissionIdentity,
                    "Every promoted admission must retain the exact authorized run identity."));
            }

            if (!TrySelectAdmissions(record, request, out var selected))
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.PromotionPlan, "The exact promotion plan is no longer eligible."));
            }

            var initiating = selected.FirstOrDefault(stored =>
                stored.Input.AdmissionId == request.InitiatingAdmissionId);
            if (initiating is null || initiating.Correlation != request.Context.Correlation)
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.AdmissionCorrelation,
                    "The initiating admission correlation differs from the proposed run."));
            }

            var reservedEntryIds = request.EntryIds
                .Insert(0, request.PromotionEntryId)
                .Add(request.AcceptedEntryId);
            if (reservedEntryIds.Any(record.EntryIds.Contains))
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.PromotionPlan, "A reserved session-entry identity is already in use."));
            }

            if (request.MessageIds.Any(record.MessageIds.Contains))
            {
                return ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartConflict(
                    SessionRunStartConflictKind.PromotionPlan, "A reserved message identity is already in use."));
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
            return ValueTask.FromResult<SessionRunStartResult>(result);
        }
    }

    private ValueTask<SessionRunStateResult> LoadRunStateCoreAsync(
        SessionRunStateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using (_gate.EnterScope())
        {
            return !TryGetAuthorizedRecord(request.Context, out var record)
                || !record.Lanes.TryGetValue(request.Context.ExecutionLaneId!.Value, out var lane)
                || lane.AcceptedState is not { } state
                || state.Correlation != request.Context.Correlation
                || state.Identity != request.Context.Identity
                ? ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateUnavailable(
                    "The requested operation state is unavailable."))
                : ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(state));
        }
    }

    private bool TryGetAuthorizedRecord(SessionOperationContext context, [NotNullWhen(true)] out SessionRecord? record)
    {
        Debug.Assert(context is not null, "A validated session operation context is required.");
        return _sessions.TryGetValue(context.ToAddress(), out record)
            && record.TenantId == context.Identity.TenantId;
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
    /// Only the lane named by <see cref="SessionOperationContext.ExecutionLaneId"/> advances, and only when its cursor
    /// is bound to <see cref="SessionAppendRequest.BranchId"/>. Session-wide appends without a lane and appends to a
    /// branch owned by a different lane leave every lane cursor untouched. The lane revision is not changed; later
    /// admission or acceptance still validates the cursor against the real branch tip.
    /// </remarks>
    private static void AdvanceOwningLaneCursor(SessionRecord record, SessionAppendRequest request)
    {
        Debug.Assert(record is not null, "A loaded session is required.");
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
    /// <param name="parent">The loaded parent branch.</param>
    /// <param name="atSequence">The requested fork sequence.</param>
    /// <returns><see langword="true"/> when <paramref name="atSequence"/> is zero or equals a committed parent-branch sequence.</returns>
    /// <remarks>
    /// Sequences are per-branch coordinates, so an identical numeric sequence on a sibling branch names a different
    /// entry, if any. Forking at such a value would not identify a committed parent on the named branch, and this
    /// check accepts only a sequence actually present in <paramref name="parent"/>'s own entry list.
    /// </remarks>
    private static bool IsCommittedForkPoint(BranchRecord parent, SessionSequence atSequence)
    {
        Debug.Assert(parent is not null, "A loaded parent branch is required.");
        return atSequence.Value == 0
            || parent.Entries.Any(entry => entry.Sequence.Value == atSequence.Value);
    }

    private static SessionBranchCursor BranchCursor(BranchId branchId, BranchRecord branch)
    {
        Debug.Assert(branch is not null, "A loaded branch is required.");
        return new SessionBranchCursor(branchId, branch.Entries.Count == 0 ? null : branch.Entries[^1].Id);
    }

    private static bool TrySelectAdmissions(SessionRecord record, SessionRunStartRequest request,
        out ImmutableArray<StoredAdmission> selected)
    {
        Debug.Assert(record is not null, "A loaded session is required.");
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
