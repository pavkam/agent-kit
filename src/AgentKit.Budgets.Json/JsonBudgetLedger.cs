// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Persists authoritative budget topology, reservations, accounting, and recovery evidence as a newline-delimited JSON journal.</summary>
/// <remarks>
/// <para>
/// The ledger is log structured. Every accounting transition is one JSON line appended and flushed to disk before the
/// in-memory projection changes and before the operation is acknowledged, so an acknowledged transition survives process
/// loss. One indivisible batch reservation is written as a single line carrying every member's evidence, allocated
/// identity, and effective expiry, which makes all-or-none reservation atomic by construction rather than by convention.
/// </para>
/// <para>
/// A journal record carries the caller's immutable evidence together with every value the ledger itself chose: allocated
/// identities, resolved expiries, and the exact clock instant the transition observed. Replay re-runs the same transition
/// logic against those recorded values, so monotonic revisions, accounting revisions, overrun generations, and recovery
/// watermarks are reconstructed identically instead of being recomputed against a later clock. A started reservation whose
/// spend is unknown is never released by expiry, disposal, or process loss; it reappears from
/// <see cref="ReadUnresolvedStartedAsync"/> and stays charged until reconciliation resolves it.
/// </para>
/// <para>
/// One in-process gate linearizes topology, accounting, replay, expiry, and recovery scans. A host-local advisory
/// exclusive lock is held for the ledger's lifetime, so a second writer on the same host fails fast instead of interleaving
/// appends. This is durable single-writer local storage: it provides no distributed lease, no fencing token, and no
/// atomicity with any external effect, which is why <see cref="Descriptor"/> reports
/// <see cref="BudgetLedgerConcurrencyDomain.ProcessLocal"/> even though it is durable.
/// </para>
/// <para>
/// Call <see cref="InitializeAsync"/> exactly once during trusted bootstrap before resolving the ledger for use.
/// </para>
/// </remarks>
public sealed partial class JsonBudgetLedger: IBudgetLedger, IDisposable
{
    private const string _storeKind = "agentkit.budgets.ledger";
    private const string _logName = "ledger";
    private const int _schemaVersion = 1;
    private static readonly BudgetLedgerDescriptor _descriptor = new(
        durable: true, concurrencyDomain: BudgetLedgerConcurrencyDomain.ProcessLocal);

    private readonly JsonBudgetLedgerTarget _target;
    private readonly JsonBudgetLedgerSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<BudgetScopeId> _scopeIds;
    private readonly IIdentifierGenerator<BudgetReservationId> _reservationIds;
    private readonly IBudgetDimensionCatalog _dimensions;
    private readonly ILogger<JsonBudgetLedger> _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private readonly Lock _gate = new();
    private readonly Dictionary<BudgetScopeId, ScopeState> _scopes = [];
    private readonly Dictionary<IdempotencyKey, ScopeState> _scopeKeys = [];
    private readonly Dictionary<BudgetReservationId, ReservationState> _reservations = [];
    private readonly Dictionary<IdempotencyKey, BatchState> _batchKeys = [];
    private readonly Dictionary<IdempotencyKey, OverrunResolutionState> _overrunResolutionKeys = [];
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _disposed;
    private bool _journalUncertain;
    private long _revision;

    /// <summary>Creates a durable ledger for one host-authorized fixed root without opening, creating, or locking it.</summary>
    /// <param name="target">The exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable evidence bounds and encoding contract.</param>
    /// <param name="timeProvider">The clock used for expiry, snapshots, start evidence, and safe duration measurement.</param>
    /// <param name="scopeIds">The replaceable source of scope identities.</param>
    /// <param name="reservationIds">The replaceable source of reservation identities.</param>
    /// <param name="dimensions">The captured dimension semantics used to validate and aggregate accounting.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    /// <remarks>Construction performs no I/O, so composition never touches the filesystem; every declared effect happens in <see cref="InitializeAsync"/>.</remarks>
    public JsonBudgetLedger(
        JsonBudgetLedgerTarget target,
        JsonBudgetLedgerSettings settings,
        TimeProvider timeProvider,
        IIdentifierGenerator<BudgetScopeId> scopeIds,
        IIdentifierGenerator<BudgetReservationId> reservationIds,
        IBudgetDimensionCatalog dimensions,
        ILogger<JsonBudgetLedger>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(scopeIds);
        ArgumentNullException.ThrowIfNull(reservationIds);
        ArgumentNullException.ThrowIfNull(dimensions);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _scopeIds = scopeIds;
        _reservationIds = reservationIds;
        _dimensions = dimensions;
        _logger = logger ?? NullLogger<JsonBudgetLedger>.Instance;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <inheritdoc/>
    public BudgetLedgerDescriptor Descriptor => _descriptor;

    /// <summary>Validates or creates the store root, binds its encoding contract, and replays the journal into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A completed task after the exact root is locked, validated, and ready for ledger operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The root, manifest, store identity, encoding contract, or persisted journal cannot be validated safely; no transition committed.</exception>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>
    /// <para>
    /// Initialization acquires the advisory exclusive lock first, so a concurrent writer is rejected before any validation
    /// observes a racing state. It then verifies the manifest's store identity, schema version, and encoding fingerprint,
    /// and performs a round-trip self-check proving the configured contract can reproduce this ledger's evidence.
    /// </para>
    /// <para>
    /// A journal ending in an incomplete append is discarded only under
    /// <see cref="JsonStoreRecoveryMode.RecoverTornAppends"/>; under <see cref="JsonStoreRecoveryMode.ValidateExact"/> it is
    /// reported as corrupt evidence. Repeating initialization after success is a no-op.
    /// </para>
    /// </remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = Run(
            "initialize",
            token =>
            {
                InitializeCore(token);
                return true;
            },
            static _ => "initialized",
            [],
            cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>An accepted scope is durable before this call returns; a rejection, conflict, or cancellation writes nothing.</remarks>
    public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(
        BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Run(
            "create_scope",
            token => CreateScopeCore(request, null, token),
            ResultOutcome,
            DiagnosticTags(request.OriginalRequest.Address),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>
    /// The whole accepted batch, including every allocated identity and effective expiry, is written as one flushed line
    /// before any member enters the projection, so recovery never observes a partially reserved batch.
    /// </remarks>
    public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(
        BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Run(
            "reserve_batch",
            token => ReserveBatchCore(request, null, token),
            ResultOutcome,
            DiagnosticTags(request.Scope.Address, request.Scope.Id, operationId: request.OriginalRequests[0].OperationId),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>Start permission is durable before the caller may begin the controlled effect, so a process lost mid-effect still recovers an unresolved started reservation.</remarks>
    public ValueTask<BudgetStartResult> MarkStartedAsync(
        BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return ValueTask.FromResult(Run(
            "mark_started",
            token => MarkStartedCore(reservation, null, token),
            ResultOutcome,
            DiagnosticTags(reservation.Scope.Address, reservation.Scope.Id, reservation.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>Settlement and the per-boundary overrun generations it creates commit in one append, so recovery never observes settled usage without its holds.</remarks>
    public ValueTask<BudgetCommitResult> SettleAsync(
        BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Run(
            "settle",
            token => SettleCore(request, null, token),
            static _ => "settled",
            DiagnosticTags(request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>A started reservation is retained rather than refunded, and that retention needs no append because nothing changed.</remarks>
    public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(
        BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return ValueTask.FromResult(Run(
            "release_unstarted",
            token => ReleaseUnstartedCore(reservation, null, token),
            ResultOutcome,
            DiagnosticTags(reservation.Scope.Address, reservation.Scope.Id, reservation.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>The replacement usage and the hold generations it creates or clears commit in one append.</remarks>
    public ValueTask<BudgetCorrectionResult> CorrectAsync(
        BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Run(
            "correct",
            token => CorrectCore(request, null, token),
            static _ => "corrected",
            DiagnosticTags(request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>The snapshot is computed before the atomic unstarted-expiry sweep, and that sweep is appended before it changes the projection.</remarks>
    public ValueTask<BudgetSnapshot> GetSnapshotAsync(
        BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ValueTask.FromResult(Run(
            "get_snapshot",
            token => GetSnapshotCore(scope, token),
            static _ => "read",
            DiagnosticTags(scope.Address, scope.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>This read makes no transition and appends nothing; watermarks are the replayed monotonic ledger revisions.</remarks>
    public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(
        BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ValueTask.FromResult(Run(
            "read_unresolved",
            token => ReadUnresolvedStartedCore(query, token),
            static _ => "read",
            DiagnosticTags(query.Scope.Address, query.Scope.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>The reconciliation binding and the settlement or release it implies commit in one append, so a replayed key never double-settles.</remarks>
    public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(
        BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Run(
            "reconcile",
            token => ReconcileCore(request, null, token),
            ResultOutcome,
            DiagnosticTags(request.Reservation.Scope.Address, request.Reservation.Scope.Id, request.Reservation.Id),
            cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">This ledger was already disposed.</exception>
    /// <remarks>Both blocked and resolved outcomes are appended, because either one binds the caller's replay key to exact evidence.</remarks>
    public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(
        BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(Run(
            "resolve_overrun_hold",
            token => ResolveOverrunHoldCore(request, null, token),
            ResultOutcome,
            DiagnosticTags(request.Hold.Boundary.Address, request.Hold.Boundary.Id, request.Hold.Reservation.Id),
            cancellationToken));
    }

    /// <summary>Releases the advisory exclusive lock held for this ledger's lifetime and drops the projection.</summary>
    /// <remarks>
    /// Disposal is idempotent and does not flush: every acknowledged record was already flushed to disk when it was
    /// appended. Started reservations with unknown spend therefore survive disposal and reappear from a recovery scan
    /// after the root is reopened. A disposed ledger serves no further operation.
    /// </remarks>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initialized = false;
            _scopes.Clear();
            _scopeKeys.Clear();
            _reservations.Clear();
            _batchKeys.Clear();
            _overrunResolutionKeys.Clear();
            _exclusive?.Dispose();
            _exclusive = null;
        }
    }

    private void InitializeCore(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
            JsonStoreRoot.ValidateFile(_root.ManifestPath);
            JsonStoreRoot.ValidateFile(_log.Path);
            _exclusive = JsonStoreLock.Acquire(_root.LockPath);
            cancellationToken.ThrowIfCancellationRequested();
            JsonStoreSerialization.VerifyRoundTrip(JsonBudgetLedgerProbe.Create(), _settings.Encoding.RecordOptions);
            cancellationToken.ThrowIfCancellationRequested();
            BindManifest(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Replay(cancellationToken);
            _initialized = true;
        }
    }

    private void BindManifest(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "The advisory exclusive lock is acquired before manifest binding.");
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw Unavailable("The configured JSON budget-ledger root has no manifest.", false);
            }

            var created = new JsonStoreManifest(
                _target.ExpectedStoreInstanceId.Value, _storeKind, _schemaVersion, _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(
                _root.ManifestPath,
                JsonStoreSerialization.Encode(created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        if (manifest.StoreId != _target.ExpectedStoreInstanceId.Value
            || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw Unavailable("The JSON budget-ledger identity does not match bootstrap configuration.", false);
        }
        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable("The JSON budget-ledger schema version is unsupported.", false);
        }
        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable("The JSON budget-ledger root was written under a different encoding contract.", false);
        }
    }

    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_scopes.Count == 0, "Replay populates an empty projection.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable("The JSON budget-ledger journal ends with an incomplete record.", false);
        }

        var retained = new List<byte[]>(replay.Records.Count);
        foreach (var record in replay.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Apply(JsonStoreSerialization.Decode<JsonBudgetLedgerRecord>(record.Span, _settings.Encoding.RecordOptions));
            retained.Add(record.ToArray());
        }

        if (replay.HasIncompleteTrailingRecord)
        {
            // The journal is never compacted: revisions, hold generations, and watermarks are derived from the complete
            // ordered history. Rewriting it here only discards the torn tail, which was never acknowledged to any caller.
            _log.Compact(retained, cancellationToken);
        }
    }

    private void Apply(JsonBudgetLedgerRecord record)
    {
        Debug.Assert(record is not null, "A decoded journal record is required.");
        try
        {
            ApplyCore(record);
        }
        catch (Exception exception) when (exception
            is (ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
            and not BudgetLedgerPersistenceUnavailableException)
        {
            throw Unavailable("A persisted JSON budget-ledger record could not be replayed into consistent state.", false, exception);
        }
    }

    private void ApplyCore(JsonBudgetLedgerRecord record)
    {
        Debug.Assert(record is not null, "A decoded journal record is required.");
        switch (record.Kind)
        {
            case JsonBudgetLedgerRecordKind.ScopeCreated:
                _ = CreateScopeCore(
                    Require(record.Scope, "scope-creation").ToDomain(), record, CancellationToken.None);
                return;
            case JsonBudgetLedgerRecordKind.BatchReserved:
                {
                    var scope = ReplayScope(record.ScopeId, "batch-reservation");
                    var entries = record.Entries;
                    if (entries.IsEmpty)
                    {
                        throw Unavailable("A persisted batch-reservation record carries no members.", false);
                    }

                    _ = ReserveBatchCore(
                        new BudgetLedgerBatchReserveRequest(
                            scope.Reference, [.. entries.Select(static entry => entry.ToDomainRequest())]),
                        record,
                        CancellationToken.None);
                    return;
                }
            case JsonBudgetLedgerRecordKind.StartMarked:
                _ = MarkStartedCore(ReplayReservation(record, "start"), record, CancellationToken.None);
                return;
            case JsonBudgetLedgerRecordKind.Released:
                _ = ReleaseUnstartedCore(ReplayReservation(record, "release"), record, CancellationToken.None);
                return;
            case JsonBudgetLedgerRecordKind.Settled:
                _ = SettleCore(
                    new BudgetLedgerSettlementRequest(
                        ReplayReservation(record, "settlement"), RequireAmount(record.Actual, "settlement")),
                    record,
                    CancellationToken.None);
                return;
            case JsonBudgetLedgerRecordKind.Corrected:
                _ = CorrectCore(
                    new BudgetLedgerCorrectionRequest(
                        ReplayReservation(record, "correction"),
                        RequireAmount(record.Actual, "correction"),
                        record.Revision ?? throw Unavailable("A persisted correction record omits its revision.", false)),
                    record,
                    CancellationToken.None);
                return;
            case JsonBudgetLedgerRecordKind.Reconciled:
                _ = ReconcileCore(
                    new BudgetLedgerReconciliationRequest(
                        ReplayReservation(record, "reconciliation"),
                        Require(record.Evidence, "reconciliation").ToDomain(),
                        new IdempotencyKey(Require(record.IdempotencyKey, "reconciliation"))),
                    record,
                    CancellationToken.None);
                return;
            case JsonBudgetLedgerRecordKind.OverrunHoldResolutionRecorded:
                {
                    var hold = Require(record.Hold, "overrun resolution");
                    var boundary = ReplayScope(hold.BoundaryScopeId, "overrun resolution");
                    var reservation = ReplayReservation(hold.ReservationId, "overrun resolution");
                    _ = ResolveOverrunHoldCore(
                        new BudgetOverrunHoldResolutionRequest(
                            hold.ToDomain(boundary.Reference, reservation),
                            Require(record.Receipt, "overrun resolution").ToDomain(),
                            new IdempotencyKey(Require(record.IdempotencyKey, "overrun resolution"))),
                        record,
                        CancellationToken.None);
                    return;
                }
            case JsonBudgetLedgerRecordKind.ExpirySwept:
                {
                    var swept = FindExpired(RequireOccurred(record, "expiry sweep"));
                    EnsureRevisionCapacity(swept.Count);
                    foreach (var expired in swept)
                    {
                        _ = Expire(expired);
                    }

                    return;
                }
            default:
                throw Unavailable("A persisted JSON budget-ledger record has an unsupported kind.", false);
        }
    }

    private BudgetLedgerScopeCreateResult CreateScopeCore(
        BudgetLedgerScopeCreateRequest request, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            if (_scopeKeys.TryGetValue(request.OriginalRequest.IdempotencyKey, out var replay))
            {
                return BudgetScopeCreateTransition.Replay(request, replay.Request, replay.Reference)!;
            }

            ScopeState? parent = null;
            if (request.OriginalRequest.ParentScopeId is { } parentId)
            {
                if (!_scopes.TryGetValue(parentId, out parent)
                    || !IsParentAddress(parent.Reference.Address, request.OriginalRequest.Address))
                {
                    throw Missing("The parent scope is unavailable.");
                }
            }

            var ancestors = ImmutableArray.CreateBuilder<BudgetLedgerScopeCreateRequest>();
            for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                ancestors.Add(ancestor.Request);
            }

            var parentEvidence = parent is null
                ? null
                : new ScopeCreateParent(parent.Reference, parent.Request, parent.Depth, ancestors.ToImmutable());
            var rejection = BudgetScopeCreateTransition.EvaluateDepth(request, parentEvidence);
            if (rejection is not null)
            {
                return rejection;
            }

            var descriptors = ImmutableArray.CreateBuilder<BudgetDimensionDescriptor?>(request.OriginalRequest.Limits.Length);
            foreach (var limit in request.OriginalRequest.Limits)
            {
                descriptors.Add(_dimensions.TryGet(limit.Dimension, out var descriptor) ? descriptor : null);
            }

            rejection = BudgetScopeCreateTransition.EvaluateLimits(request, parentEvidence, descriptors.MoveToImmutable());
            if (rejection is not null)
            {
                return rejection;
            }

            var id = journal is null
                ? _scopeIds.Create()
                : new BudgetScopeId(journal.ScopeId ?? throw Unavailable("A persisted scope record omits its identity.", false));
            ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
            EnsureRevisionCapacity(1);
            var (result, mutation) = BudgetScopeCreateTransition.PlanAccepted(
                request, parentEvidence, id, _scopes.ContainsKey(id), _revision + 1);
            cancellationToken.ThrowIfCancellationRequested();
            if (journal is null)
            {
                AppendRecord(JsonBudgetLedgerRecord.ForScopeCreated(request, id), cancellationToken);
            }

            var state = new ScopeState(mutation.Reference, mutation.Request, parent, mutation.Depth);
            _scopes.Add(id, state);
            _scopeKeys.Add(request.OriginalRequest.IdempotencyKey, state);
            _ = AdvanceRevision();
            return result;
        }
    }

    private BudgetLedgerBatchReserveResult ReserveBatchCore(
        BudgetLedgerBatchReserveRequest request, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            var scope = GetScope(request.Scope);
            BatchState? replay = null;
            foreach (var item in request.OriginalRequests)
            {
                if (_batchKeys.TryGetValue(item.IdempotencyKey, out var found))
                {
                    if (replay is not null && !ReferenceEquals(replay, found))
                    {
                        throw Conflict("Batch item keys belong to different batches.");
                    }

                    replay = found;
                }
            }
            if (replay is not null)
            {
                return replay.Request != request
                    || request.OriginalRequests.Any(item => !_batchKeys.ContainsKey(item.IdempotencyKey))
                    ? throw Conflict("A batch item key is bound to different ordered evidence.")
                    : replay.Result;
            }

            var descriptors = ImmutableArray.CreateBuilder<BudgetDimensionDescriptor>(request.OriginalRequests.Length);
            foreach (var item in request.OriginalRequests)
            {
                if (!_dimensions.TryGet(item.Dimension, out var descriptor) || !descriptor.AllowedUnits.Contains(item.Unit))
                {
                    throw new BudgetLedgerStateException("The reservation has no compatible dimension descriptor.");
                }
                if (!Enum.IsDefined(descriptor.Aggregation))
                {
                    throw new BudgetLedgerStateException("The reservation dimension has undefined aggregation semantics.");
                }
                if (request.OriginalRequests.Any(candidate => candidate.Dimension == item.Dimension && candidate.Unit != item.Unit))
                {
                    throw new BudgetLedgerStateException("One atomic batch cannot mix units for the same dimension.");
                }

                descriptors.Add(descriptor);
            }

            var lineage = Lineage(scope);
            foreach (var boundary in lineage)
            {
                foreach (var item in request.OriginalRequests)
                {
                    if (boundary.Reservations.Any(existing =>
                            existing.Receipt.OriginalRequest.Dimension == item.Dimension
                            && existing.Receipt.OriginalRequest.Unit != item.Unit))
                    {
                        throw new BudgetLedgerStateException(
                            "The reservation unit conflicts with existing accounting at a charged boundary.");
                    }
                }
            }

            var requestedDimensions = request.OriginalRequests.Select(item => item.Dimension).ToHashSet();
            var activeHolds = lineage
                .SelectMany(boundary => ActiveHoldEvidence(boundary))
                .Where(hold => requestedDimensions.Contains(hold.Dimension))
                .ToImmutableArray();
            if (!activeHolds.IsEmpty)
            {
                return new BudgetLedgerBatchReserveHeld(activeHolds);
            }

            var now = journal is null ? _timeProvider.GetUtcNow() : RequireOccurred(journal, "batch reservation");
            var expired = FindExpired(now);
            foreach (var boundary in lineage)
            {
                var open = boundary.Reservations.Count(item => item.IsCapacityRetaining && !expired.Contains(item));
                if (open + request.OriginalRequests.Length > boundary.Request.Admission.MaximumOpenReservationsPerScope)
                {
                    throw new BudgetLedgerStateException(
                        "The atomic batch would exceed the scope's captured open-reservation capacity.");
                }

                foreach (var group in request.OriginalRequests.GroupBy(item => (item.Dimension, item.Unit)))
                {
                    var limit = boundary.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == group.Key.Dimension);
                    if (limit is not null && limit.Unit != group.Key.Unit)
                    {
                        throw new BudgetLedgerStateException("The reservation unit conflicts with a captured scope limit.");
                    }

                    var descriptor = descriptors.First(item => item.Dimension == group.Key.Dimension);
                    var (reserved, committed) = Usage(boundary, group.Key.Dimension, expired);
                    var observed = descriptor.Aggregation == BudgetAggregationKind.Maximum
                        ? Max(reserved, committed)
                        : reserved.Add(committed);
                    var amount = descriptor.Aggregation switch
                    {
                        BudgetAggregationKind.Maximum => BudgetQuantity.FromDecimal(group.Max(item => item.Amount)),
                        BudgetAggregationKind.ConcurrentGauge or BudgetAggregationKind.Sum or BudgetAggregationKind.Duration =>
                            Sum(group.Select(item => item.Amount)),
                        _ => throw new BudgetLedgerStateException("The dimension descriptor has an unsupported aggregation kind."),
                    };
                    var projected = descriptor.Aggregation == BudgetAggregationKind.Maximum
                        ? Max(observed, amount)
                        : observed.Add(amount);
                    if (limit is { Kind: BudgetLimitKind.Hard } && projected.CompareTo(BudgetQuantity.FromDecimal(limit.Value)) > 0)
                    {
                        return Reject(boundary, group.First(), limit.Value, observed, amount, limit.Unit.Value);
                    }
                }
            }

            var entries = journal?.Entries ?? [];
            if (journal is not null && entries.Length != request.OriginalRequests.Length)
            {
                throw Unavailable("A persisted batch-reservation record does not match its ordered members.", false);
            }

            var receiptsBuilder = ImmutableArray.CreateBuilder<BudgetLedgerReservationReceipt>(request.OriginalRequests.Length);
            var newIds = new HashSet<BudgetReservationId>();
            for (var index = 0; index < request.OriginalRequests.Length; index++)
            {
                var item = request.OriginalRequests[index];
                var id = journal is null ? _reservationIds.Create() : entries[index].ToDomainReservationId();
                ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
                if (_reservations.ContainsKey(id) || !newIds.Add(id))
                {
                    throw new BudgetLedgerStateException("The reservation identity source produced a duplicate value.");
                }

                var reference = new BudgetLedgerReservationReference(scope.Reference, id);
                var effectiveExpiry = journal is null
                    ? item.ExpiresAt ?? (now + scope.Request.Admission.DefaultReservationLifetime)
                    : entries[index].EffectiveExpiresAt;
                receiptsBuilder.Add(new BudgetLedgerReservationReceipt(
                    reference, item, new BudgetEffectiveReservation(effectiveExpiry)));
            }

            var receipts = receiptsBuilder.ToImmutable();
            var result = new BudgetLedgerBatchReserved(receipts);
            var batch = new BatchState(request, result);
            EnsureRevisionCapacity(expired.Count + 1);
            cancellationToken.ThrowIfCancellationRequested();
            if (journal is null)
            {
                AppendRecord(JsonBudgetLedgerRecord.ForBatchReserved(scope.Reference.Id, receipts, now), cancellationToken);
            }

            foreach (var expiredReservation in expired)
            {
                _ = Expire(expiredReservation);
            }

            for (var index = 0; index < receipts.Length; index++)
            {
                var receipt = receipts[index];
                var reservation = new ReservationState(receipt, lineage, descriptors[index].Aggregation);
                _reservations.Add(receipt.Reservation.Id, reservation);
                foreach (var boundary in lineage)
                {
                    boundary.Reservations.Add(reservation);
                }
            }

            foreach (var item in request.OriginalRequests)
            {
                _batchKeys.Add(item.IdempotencyKey, batch);
            }

            _ = AdvanceRevision();
            return result;
        }
    }

    private BudgetStartResult MarkStartedCore(
        BudgetLedgerReservationReference reservation, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(reservation is not null, "The public wrapper validates the reservation reference before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(reservation);
            if (state.Commit is not null || state.Released)
            {
                return state.StartExpiration
                    ?? throw new BudgetLedgerStateException("The reservation is terminal and cannot start.");
            }
            if (state.StartedAt is not null)
            {
                return new BudgetStarted(reservation.Id, true);
            }

            var now = journal is null ? _timeProvider.GetUtcNow() : RequireOccurred(journal, "start");
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRevisionCapacity(1);
            if (journal is null)
            {
                AppendRecord(JsonBudgetLedgerRecord.ForStartMarked(reservation.Id, now), cancellationToken);
            }
            if (now >= state.Receipt.EffectiveReservation.ExpiresAt)
            {
                return Expire(state);
            }

            state.StartedAt = now;
            state.StartRevision = AdvanceRevision();
            return new BudgetStarted(reservation.Id, false);
        }
    }

    private BudgetCommitResult SettleCore(
        BudgetLedgerSettlementRequest request, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(request.Reservation);
            return Settle(state, request.Actual, journal is null, cancellationToken);
        }
    }

    private BudgetLedgerReleaseResult ReleaseUnstartedCore(
        BudgetLedgerReservationReference reservation, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(reservation is not null, "The public wrapper validates the reservation reference before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(reservation);
            if (state.Commit is not null)
            {
                return new BudgetLedgerAlreadySettled(state.Commit);
            }
            if (state.StartedAt is not null)
            {
                return new BudgetLedgerRetainedStarted(reservation);
            }
            if (state.Released)
            {
                return new BudgetLedgerReleased(reservation);
            }

            EnsureRevisionCapacity(1);
            cancellationToken.ThrowIfCancellationRequested();
            if (journal is null)
            {
                AppendRecord(JsonBudgetLedgerRecord.ForReleased(reservation.Id), cancellationToken);
            }

            Release(state);
            return new BudgetLedgerReleased(reservation);
        }
    }

    private BudgetCorrectionResult CorrectCore(
        BudgetLedgerCorrectionRequest request, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(request.Reservation);
            if (state.Commit is null)
            {
                throw new BudgetLedgerStateException("Only settled accounting can be corrected.");
            }
            if (state.Corrections.TryGetValue(request.Revision, out var replay))
            {
                return replay.CorrectedActual != request.CorrectedActual
                    ? throw Conflict("The correction revision is bound to different usage.")
                    : replay;
            }
            if (request.Revision <= state.LatestCorrectionRevision)
            {
                throw new BudgetLedgerStateException("Correction revisions must increase monotonically.");
            }

            EnsureRevisionCapacity(1);
            var accountingRevision = new BudgetAccountingRevision(checked(_revision + 1));
            var previousActual = state.Commit.Actual;
            var reserved = state.Receipt.OriginalRequest.Amount;
            var createdStates = ImmutableArray.CreateBuilder<(ScopeState Boundary, OverrunHoldState Hold)>();
            if (previousActual <= reserved && request.CorrectedActual > reserved)
            {
                foreach (var boundary in state.Lineage.Where(boundary => !boundary.OverrunHolds.Any(hold =>
                    hold.IsActive && hold.Evidence.Reference.Reservation == state.Receipt.Reservation)))
                {
                    createdStates.Add((boundary, new OverrunHoldState(
                        HoldEvidence(boundary, state, accountingRevision, request.CorrectedActual))));
                }
            }

            var now = journal is null ? _timeProvider.GetUtcNow() : RequireOccurred(journal, "correction");
            var expiredForClearing = FindExpired(now);
            var clearing = state.Lineage
                .SelectMany(boundary => EligibleForClear(boundary, state, request.CorrectedActual, expiredForClearing)
                    ? boundary.OverrunHolds.Where(hold =>
                        hold.IsActive
                        && hold.Evidence.Policy == BudgetOverrunHoldPolicy.ClearWhenReconciled
                        && hold.Evidence.Dimension == state.Receipt.OriginalRequest.Dimension)
                    : [])
                .ToImmutableArray();
            var result = new BudgetCorrectionResult(
                request.Reservation.Id,
                previousActual,
                request.CorrectedActual,
                request.Revision,
                accountingRevision,
                [.. createdStates.Select(item => item.Hold.Evidence)],
                [.. clearing.Select(item => item.Evidence.Reference)]);
            var commit = Commit(
                request.Reservation.Id,
                reserved,
                request.CorrectedActual,
                accountingRevision,
                [.. createdStates.Select(item => item.Hold.Evidence)]);
            cancellationToken.ThrowIfCancellationRequested();
            if (journal is null)
            {
                AppendRecord(
                    JsonBudgetLedgerRecord.ForCorrected(
                        request.Reservation.Id, request.CorrectedActual, request.Revision, now),
                    cancellationToken);
            }

            state.Commit = commit;
            state.AccountingRevision = accountingRevision;
            state.LatestCorrectionRevision = request.Revision;
            state.Corrections.Add(request.Revision, result);
            foreach (var (boundary, hold) in createdStates)
            {
                boundary.OverrunHolds.Add(hold);
            }
            foreach (var hold in clearing)
            {
                hold.AutomaticallyCleared = true;
            }

            _ = AdvanceRevision();
            return result;
        }
    }

    private BudgetSnapshot GetSnapshotCore(BudgetLedgerScopeReference scope, CancellationToken cancellationToken)
    {
        Debug.Assert(scope is not null, "The public wrapper validates the scope reference before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(null);
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetScope(scope);
            var now = _timeProvider.GetUtcNow();
            var expired = FindExpired(now);
            var dimensions = state.Request.OriginalRequest.Limits
                .Select(item => item.Dimension)
                .Concat(state.Reservations.Select(item => item.Receipt.OriginalRequest.Dimension))
                .Distinct();
            var usages = dimensions.Select(dimension =>
            {
                var (reserved, committed) = Usage(state, dimension, expired);
                var limit = state.Request.OriginalRequest.Limits.FirstOrDefault(item => item.Dimension == dimension);
                var unit = limit?.Unit
                    ?? state.Reservations.First(item => item.Receipt.OriginalRequest.Dimension == dimension)
                        .Receipt.OriginalRequest.Unit;
                return new BudgetDimensionUsage(dimension, unit, reserved, committed, limit);
            }).ToImmutableArray();
            var snapshot = new BudgetSnapshot(scope.Id, now, usages, ActiveHoldEvidence(state));
            EnsureRevisionCapacity(expired.Count);
            cancellationToken.ThrowIfCancellationRequested();
            if (expired.Count > 0)
            {
                AppendRecord(JsonBudgetLedgerRecord.ForExpirySweep(now), cancellationToken);
                foreach (var expiredReservation in expired)
                {
                    _ = Expire(expiredReservation);
                }
            }

            return snapshot;
        }
    }

    private BudgetUnresolvedReservationPage ReadUnresolvedStartedCore(
        BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken)
    {
        Debug.Assert(query is not null, "The public wrapper validates the query before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(null);
            cancellationToken.ThrowIfCancellationRequested();
            var scope = GetScope(query.Scope);
            if (query.PageSize > scope.Request.Admission.MaximumOpenReservationsPerScope)
            {
                throw new BudgetLedgerStateException("The page size exceeds the captured finite ledger bound.");
            }

            var watermark = query.After?.Watermark ?? new BudgetLedgerWatermark(Math.Max(1, _revision));
            if (watermark.Value > _revision)
            {
                throw new BudgetLedgerStateException("The scan watermark is not known to this ledger state.");
            }
            if (query.After is { } cursor)
            {
                if (!_reservations.TryGetValue(cursor.AfterReservationId, out var cursorReservation)
                    || cursorReservation.Receipt.Reservation.Scope != query.Scope
                    || cursorReservation.StartedAt is null
                    || cursorReservation.StartRevision > watermark.Value)
                {
                    throw new BudgetLedgerStateException("The scan cursor is not valid at its anchored watermark.");
                }
            }

            var rows = scope.Reservations
                .Where(item => item.Receipt.Reservation.Scope == query.Scope
                    && item.StartedAt is not null
                    && item.Commit is null
                    && !item.Released
                    && item.StartRevision <= watermark.Value)
                .OrderBy(item => item.Receipt.Reservation.Id.Value.ToString("D"), StringComparer.Ordinal)
                .ToArray();
            if (query.After is not null)
            {
                rows = [.. rows.Where(item => string.CompareOrdinal(
                    item.Receipt.Reservation.Id.Value.ToString("D"),
                    query.After.AfterReservationId.Value.ToString("D")) > 0)];
            }

            var selected = rows
                .Take(query.PageSize)
                .Select(item => new BudgetUnresolvedReservation(item.Receipt, item.StartedAt!.Value))
                .ToImmutableArray();
            var next = rows.Length > selected.Length && selected.Length > 0
                ? new BudgetReservationCursor(query.Scope, watermark, selected[^1].Receipt.Reservation.Id)
                : null;
            return new BudgetUnresolvedReservationPage(query.Scope, watermark, selected, next);
        }
    }

    private BudgetLedgerReconciliationResult ReconcileCore(
        BudgetLedgerReconciliationRequest request, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            var state = GetReservation(request.Reservation);
            if (state.Reconciliations.TryGetValue(request.IdempotencyKey, out var saved))
            {
                return saved.Evidence != request.Evidence
                    ? throw Conflict("The reconciliation key is bound to different evidence.")
                    : saved.Result;
            }
            if (state.Commit is not null || state.Released)
            {
                throw new BudgetLedgerStateException("Fresh reconciliation evidence cannot target a terminal reservation.");
            }
            if (state.StartedAt is null)
            {
                throw new BudgetLedgerStateException("Only a started reservation can be reconciled.");
            }

            EnsureRevisionCapacity(request.Evidence is BudgetStillUnknown ? 1 : 2);
            cancellationToken.ThrowIfCancellationRequested();
            if (journal is null)
            {
                AppendRecord(
                    JsonBudgetLedgerRecord.ForReconciled(request.Reservation.Id, request.Evidence, request.IdempotencyKey),
                    cancellationToken);
            }

            // The reconciliation record already committed the settlement or release this evidence implies, so the nested
            // transition must neither append its own record nor observe cancellation after that durable append.
            BudgetLedgerReconciliationResult result = request.Evidence switch
            {
                BudgetActualMeasured measured => new BudgetLedgerReconciliationSettled(
                    Settle(state, measured.Actual, append: false, CancellationToken.None)),
                BudgetActualEstimated estimated => new BudgetLedgerReconciliationSettled(
                    Settle(state, estimated.Actual, append: false, CancellationToken.None)),
                BudgetNoUsageProven => ReleaseForReconciliation(state),
                BudgetStillUnknown => new BudgetLedgerReconciliationRetainedUnknown(request.Reservation),
                _ => throw new BudgetLedgerStateException("The reconciliation evidence category is not supported."),
            };
            state.Reconciliations.Add(request.IdempotencyKey, new ReconciliationState(request.Evidence, result));
            _ = AdvanceRevision();
            return result;
        }
    }

    private BudgetOverrunHoldResolutionResult ResolveOverrunHoldCore(
        BudgetOverrunHoldResolutionRequest request, JsonBudgetLedgerRecord? journal, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public wrapper validates the request before dispatch.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireUsable(journal);
            cancellationToken.ThrowIfCancellationRequested();
            if (_overrunResolutionKeys.TryGetValue(request.IdempotencyKey, out var replay))
            {
                return replay.Request != request
                    ? throw Conflict("The overrun-resolution key is bound to different evidence.")
                    : replay.Result;
            }

            var boundary = GetScope(request.Hold.Boundary);
            var reservation = GetReservation(request.Hold.Reservation);
            if (!reservation.Lineage.Contains(boundary))
            {
                throw Missing("The overrun hold reference is unavailable.");
            }

            var hold = boundary.OverrunHolds.FirstOrDefault(item => item.Evidence.Reference == request.Hold)
                ?? throw new BudgetLedgerStateException("The overrun hold generation is stale or unavailable.");
            if (hold.Resolution is not null)
            {
                throw new BudgetLedgerStateException("A fresh key cannot resolve an already terminal overrun hold generation.");
            }
            if (hold.Evidence.Policy != BudgetOverrunHoldPolicy.RequireAuthorizedResolution || !hold.IsActive)
            {
                throw new BudgetLedgerStateException("The overrun hold generation cannot accept operator resolution.");
            }
            if (!BudgetOverrunSecurityBinding.Matches(request.Hold, request.EnforcementReceipt))
            {
                throw new BudgetLedgerStateException("The enforcement receipt is not structurally bound to the requested hold.");
            }

            var now = journal is null ? _timeProvider.GetUtcNow() : RequireOccurred(journal, "overrun resolution");
            var currentOverruns = CurrentOverruns(boundary, hold.Evidence.Dimension);
            var hardFailures = CurrentHardFailures(boundary, hold.Evidence.Dimension, FindExpired(now));
            EnsureRevisionCapacity(1);
            BudgetOverrunHoldResolutionResult result = !currentOverruns.IsEmpty || !hardFailures.IsEmpty
                ? new BudgetOverrunHoldResolutionBlocked(request.Hold, currentOverruns, hardFailures)
                : new BudgetOverrunHoldResolved(
                    request.Hold, new BudgetAccountingRevision(checked(_revision + 1)), request.EnforcementReceipt);
            cancellationToken.ThrowIfCancellationRequested();
            if (journal is null)
            {
                AppendRecord(JsonBudgetLedgerRecord.ForOverrunHoldResolution(request, now), cancellationToken);
            }
            if (result is BudgetOverrunHoldResolved resolved)
            {
                hold.Resolution = resolved;
            }

            _overrunResolutionKeys.Add(request.IdempotencyKey, new OverrunResolutionState(request, result));
            _ = AdvanceRevision();
            return result;
        }
    }

    private BudgetCommitResult Settle(
        ReservationState state, decimal actual, bool append, CancellationToken cancellationToken)
    {
        Debug.Assert(state is not null, "The caller resolves reservation state before settlement.");
        Debug.Assert(actual >= 0, "The settlement request validates nonnegative actual usage.");
        if (state.OriginalCommit is not null)
        {
            return state.OriginalCommit.Actual != actual
                ? throw Conflict("The reservation is settled with different actual usage.")
                : state.OriginalCommit;
        }
        if (state.StartedAt is null || state.Released)
        {
            throw new BudgetLedgerStateException("The reservation is not started or is released.");
        }

        EnsureRevisionCapacity(1);
        var accountingRevision = new BudgetAccountingRevision(checked(_revision + 1));
        var createdStates = ImmutableArray.CreateBuilder<(ScopeState Boundary, OverrunHoldState Hold)>();
        if (actual > state.Receipt.OriginalRequest.Amount)
        {
            foreach (var boundary in state.Lineage)
            {
                createdStates.Add((boundary, new OverrunHoldState(HoldEvidence(boundary, state, accountingRevision, actual))));
            }
        }

        var createdEvidence = createdStates.Select(item => item.Hold.Evidence).ToImmutableArray();
        var commit = Commit(
            state.Receipt.Reservation.Id, state.Receipt.OriginalRequest.Amount, actual, accountingRevision, createdEvidence);
        cancellationToken.ThrowIfCancellationRequested();
        if (append)
        {
            AppendRecord(JsonBudgetLedgerRecord.ForSettled(state.Receipt.Reservation.Id, actual), cancellationToken);
        }

        state.Commit = commit;
        state.OriginalCommit = commit;
        state.AccountingRevision = accountingRevision;
        foreach (var (boundary, hold) in createdStates)
        {
            boundary.OverrunHolds.Add(hold);
        }

        _ = AdvanceRevision();
        return commit;
    }

    private void AppendRecord(JsonBudgetLedgerRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A journal record is required before a projection mutation.");
        byte[] payload;
        try
        {
            payload = JsonStoreSerialization.Encode(record, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or NotSupportedException)
        {
            throw Unavailable(
                "The JSON budget ledger could not encode its transition record within the configured bounds.",
                false,
                exception);
        }

        try
        {
            _log.Append(payload, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception
            is IOException or UnauthorizedAccessException or NotSupportedException
            or InvalidDataException or ArgumentOutOfRangeException)
        {
            // The write may have reached the journal before failing, so the projection is no longer provably consistent
            // with durable state and this ledger instance stops serving rather than answering from a divergent view.
            _journalUncertain = true;
            throw Unavailable(
                "The JSON budget ledger could not confirm a durable append of its transition record.", true, exception);
        }
    }

    private void RequireUsable(JsonBudgetLedgerRecord? journal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (journal is not null)
        {
            return;
        }
        if (!_initialized)
        {
            throw Unavailable("The JSON budget ledger was used before trusted bootstrap initialization.", false);
        }
        if (_journalUncertain)
        {
            throw Unavailable(
                "A previous JSON budget-ledger append could not be confirmed, so this instance no longer serves operations.",
                true);
        }
    }

    private ScopeState ReplayScope(Guid? scopeId, string context)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return scopeId is { } value && _scopes.TryGetValue(new BudgetScopeId(value), out var scope)
            ? scope
            : throw Unavailable($"A persisted {context} record references an unknown scope.", false);
    }

    private BudgetLedgerReservationReference ReplayReservation(JsonBudgetLedgerRecord record, string context)
    {
        Debug.Assert(record is not null, "A decoded journal record is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return ReplayReservation(
            record.ReservationId ?? throw Unavailable($"A persisted {context} record omits its reservation.", false),
            context);
    }

    private BudgetLedgerReservationReference ReplayReservation(Guid reservationId, string context)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return _reservations.TryGetValue(new BudgetReservationId(reservationId), out var reservation)
            ? reservation.Receipt.Reservation
            : throw Unavailable($"A persisted {context} record references an unknown reservation.", false);
    }

    private static DateTimeOffset RequireOccurred(JsonBudgetLedgerRecord record, string context)
    {
        Debug.Assert(record is not null, "A decoded journal record is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return record.Occurred
            ?? throw Unavailable($"A persisted {context} record omits its captured clock instant.", false);
    }

    private static decimal RequireAmount(decimal? amount, string context)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return amount is { } value && value >= 0
            ? value
            : throw Unavailable($"A persisted {context} record omits its nonnegative usage.", false);
    }

    private static T Require<T>(T? value, string context)
        where T : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value ?? throw Unavailable($"A persisted {context} record omits its required payload.", false);
    }

    private ScopeState GetScope(BudgetLedgerScopeReference reference)
    {
        Debug.Assert(reference is not null, "The caller supplies a validated exact scope reference.");
        return _scopes.TryGetValue(reference.Id, out var scope) && scope.Reference == reference
            ? scope
            : throw Missing("The scope reference is unavailable.");
    }

    private ReservationState GetReservation(BudgetLedgerReservationReference reference)
    {
        Debug.Assert(reference is not null, "The caller supplies a validated exact reservation reference.");
        return _reservations.TryGetValue(reference.Id, out var reservation) && reservation.Receipt.Reservation == reference
            ? reservation
            : throw Missing("The reservation reference is unavailable.");
    }

    private static ImmutableArray<ScopeState> Lineage(ScopeState scope)
    {
        Debug.Assert(scope is not null, "The caller resolves the target scope before traversing its lineage.");
        var builder = ImmutableArray.CreateBuilder<ScopeState>();
        for (var current = scope; current is not null; current = current.Parent)
        {
            builder.Add(current);
        }

        return builder.ToImmutable();
    }

    private HashSet<ReservationState> FindExpired(DateTimeOffset now) =>
    [
        .. _reservations.Values.Where(item =>
            item.StartedAt is null && item.Commit is null && !item.Released
            && now >= item.Receipt.EffectiveReservation.ExpiresAt),
    ];

    private void Release(ReservationState state)
    {
        Debug.Assert(state is not null, "The caller resolves reservation state before release.");
        if (state.Released)
        {
            return;
        }

        state.Released = true;
        _ = AdvanceRevision();
    }

    private BudgetStartExpired Expire(ReservationState state)
    {
        Debug.Assert(state is not null, "The caller resolves expired reservation state before cleanup.");
        Debug.Assert(state.StartedAt is null && state.Commit is null, "Only unresolved unstarted reservations expire.");
        state.StartExpiration ??= new BudgetStartExpired(
            state.Receipt.Reservation.Id, state.Receipt.EffectiveReservation.ExpiresAt);
        Release(state);
        return state.StartExpiration;
    }

    private BudgetLedgerReconciliationReleased ReleaseForReconciliation(ReservationState state)
    {
        Debug.Assert(state is not null, "The reconciliation path resolves reservation state before release.");
        Release(state);
        return new BudgetLedgerReconciliationReleased(state.Receipt.Reservation);
    }

    private static BudgetCommitResult Commit(
        BudgetReservationId id,
        decimal reserved,
        decimal actual,
        BudgetAccountingRevision accountingRevision,
        ImmutableArray<BudgetOverrunHold> createdHolds)
    {
        Debug.Assert(id != default, "The persisted reservation supplies a nondefault identity.");
        Debug.Assert(reserved >= 0, "The original reservation request validates nonnegative capacity.");
        Debug.Assert(actual >= 0, "The settlement request validates nonnegative actual usage.");
        return new BudgetCommitResult(
            id, reserved, actual, Math.Max(0, reserved - actual), Math.Max(0, actual - reserved),
            accountingRevision, createdHolds);
    }

    private long AdvanceRevision() => _revision = checked(_revision + 1);

    private void EnsureRevisionCapacity(int transitions)
    {
        Debug.Assert(transitions >= 0, "The caller preflights a nonnegative transition count.");
        _ = checked(_revision + transitions);
    }

    private static (BudgetQuantity Reserved, BudgetQuantity Committed) Usage(
        ScopeState scope, BudgetDimension dimension, HashSet<ReservationState>? excluded = null)
    {
        Debug.Assert(scope is not null, "The caller resolves scope state before aggregating usage.");
        Debug.Assert(dimension != default, "The caller supplies a validated budget dimension.");
        var reservations = scope.Reservations
            .Where(item => item.Receipt.OriginalRequest.Dimension == dimension).ToArray();
        var retained = reservations
            .Where(item => !item.Released && item.Commit is null && excluded?.Contains(item) != true).ToArray();
        var aggregation = reservations.FirstOrDefault()?.Aggregation ?? BudgetAggregationKind.Sum;
        var reserved = aggregation switch
        {
            BudgetAggregationKind.Maximum => retained.Length == 0
                ? default
                : BudgetQuantity.FromDecimal(retained.Max(item => item.Receipt.OriginalRequest.Amount)),
            BudgetAggregationKind.ConcurrentGauge or BudgetAggregationKind.Sum or BudgetAggregationKind.Duration =>
                Sum(retained.Select(item => item.Receipt.OriginalRequest.Amount)),
            _ => throw new BudgetLedgerStateException("The retained accounting has an unsupported aggregation kind."),
        };
        var settled = reservations.Where(item => item.Commit is not null).Select(item => item.Commit!.Actual).ToArray();
        var committed = aggregation switch
        {
            BudgetAggregationKind.Maximum => settled.Length == 0 ? default : BudgetQuantity.FromDecimal(settled.Max()),
            BudgetAggregationKind.ConcurrentGauge => default,
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => Sum(settled),
            _ => throw new BudgetLedgerStateException("The committed accounting has an unsupported aggregation kind."),
        };
        return (reserved, committed);
    }

    private static BudgetLedgerBatchReserveRejected Reject(
        ScopeState scope,
        BudgetReservationRequest item,
        decimal limit,
        BudgetQuantity observed,
        BudgetQuantity requested,
        string? unit = null)
    {
        Debug.Assert(scope is not null, "The caller resolves the rejecting scope boundary.");
        Debug.Assert(item is not null, "The caller selects one validated batch item for dimension metadata.");
        Debug.Assert(limit >= 0, "Captured limits are nonnegative.");
        return new BudgetLedgerBatchReserveRejected(new BudgetLimitFailure(
            scope.Reference.Id,
            item.Dimension,
            BudgetLimitKind.Hard,
            limit,
            observed,
            requested,
            unit is null ? item.Unit : new BudgetUnit(unit),
            "The atomic batch would exceed a captured hard boundary."));
    }

    private static BudgetQuantity Sum(IEnumerable<decimal> values)
    {
        Debug.Assert(values is not null, "The caller supplies a non-null sequence of validated quantities.");
        return values.Aggregate(default(BudgetQuantity), static (total, value) => total.Add(BudgetQuantity.FromDecimal(value)));
    }

    private static BudgetQuantity Max(BudgetQuantity left, BudgetQuantity right) => left.CompareTo(right) >= 0 ? left : right;

    private static BudgetOverrunHold HoldEvidence(
        ScopeState boundary, ReservationState reservation, BudgetAccountingRevision revision, decimal actual)
    {
        Debug.Assert(boundary is not null, "The caller resolves the charged boundary.");
        Debug.Assert(reservation is not null, "The caller resolves the triggering reservation.");
        Debug.Assert(actual > reservation.Receipt.OriginalRequest.Amount, "A hold is created only for a current row overrun.");
        var original = reservation.Receipt.OriginalRequest;
        return new BudgetOverrunHold(
            new BudgetOverrunHoldReference(boundary.Reference, reservation.Receipt.Reservation, revision),
            original.Dimension,
            original.Unit,
            original.Amount,
            actual,
            boundary.Request.Admission.OverrunHoldPolicy);
    }

    private ImmutableArray<BudgetOverrunHold> ActiveHoldEvidence(ScopeState boundary)
    {
        Debug.Assert(boundary is not null, "The caller resolves a charged boundary.");
        return
        [
            .. boundary.OverrunHolds
                .Where(hold => hold.IsActive)
                .Select(hold => CurrentEvidence(hold.Evidence))
                .OrderBy(hold => hold.Reference.TriggeringRevision.Value),
        ];
    }

    private BudgetOverrunHold CurrentEvidence(BudgetOverrunHold evidence)
    {
        Debug.Assert(evidence is not null, "Persisted hold evidence is non-null.");
        var state = _reservations[evidence.Reference.Reservation.Id];
        return new BudgetOverrunHold(
            evidence.Reference,
            evidence.Dimension,
            evidence.Unit,
            evidence.Reserved,
            state.Commit?.Actual ?? evidence.CurrentActual,
            evidence.Policy);
    }

    private ImmutableArray<BudgetOverrunHold> CurrentOverruns(ScopeState boundary, BudgetDimension dimension)
    {
        Debug.Assert(boundary is not null, "The caller resolves the hold-owning boundary.");
        return
        [
            .. boundary.OverrunHolds
                .Where(hold => hold.IsActive && hold.Evidence.Dimension == dimension)
                .Select(hold => CurrentEvidence(hold.Evidence))
                .Where(hold => hold.CurrentActual > hold.Reserved),
        ];
    }

    private static bool EligibleForClear(
        ScopeState boundary,
        ReservationState correcting,
        decimal correctedActual,
        HashSet<ReservationState> expired)
    {
        Debug.Assert(boundary is not null, "The correction supplies a charged boundary.");
        Debug.Assert(correcting is not null, "The correction supplies settled reservation state.");
        Debug.Assert(expired is not null, "The caller supplies the current expired-reservation set.");
        var original = correcting.Receipt.OriginalRequest;
        if (boundary.Reservations.Any(item =>
            item.Commit is not null
            && item.Receipt.OriginalRequest.Dimension == original.Dimension
            && (ReferenceEquals(item, correcting) ? correctedActual : item.Commit.Actual) > item.Receipt.OriginalRequest.Amount))
        {
            return false;
        }

        var hard = boundary.Request.OriginalRequest.Limits.FirstOrDefault(limit =>
            limit.Dimension == original.Dimension && limit.Kind == BudgetLimitKind.Hard);
        if (hard is null)
        {
            return true;
        }

        var settledValues = boundary.Reservations
            .Where(item => item.Commit is not null && item.Receipt.OriginalRequest.Dimension == original.Dimension)
            .Select(item => ReferenceEquals(item, correcting) ? correctedActual : item.Commit!.Actual)
            .ToArray();
        // Expired-but-unswept unstarted reservations no longer retain capacity: expiry is applied lazily on
        // reserve, snapshot, and mark-started, so without this exclusion a dead reservation could inflate the
        // observed total past the hard ceiling and keep a clearable hold stuck open forever.
        var retainedValues = boundary.Reservations
            .Where(item => item.IsCapacityRetaining
                && !expired.Contains(item)
                && item.Receipt.OriginalRequest.Dimension == original.Dimension)
            .Select(item => item.Receipt.OriginalRequest.Amount)
            .ToArray();
        var observed = correcting.Aggregation switch
        {
            BudgetAggregationKind.Maximum => Max(
                retainedValues.Length == 0 ? default : BudgetQuantity.FromDecimal(retainedValues.Max()),
                settledValues.Length == 0 ? default : BudgetQuantity.FromDecimal(settledValues.Max())),
            BudgetAggregationKind.ConcurrentGauge => Sum(retainedValues),
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => Sum(retainedValues).Add(Sum(settledValues)),
            _ => throw new BudgetLedgerStateException("The corrected accounting has unsupported aggregation semantics."),
        };
        return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0;
    }

    private static ImmutableArray<BudgetLimitFailure> CurrentHardFailures(
        ScopeState boundary, BudgetDimension dimension, HashSet<ReservationState> expired)
    {
        Debug.Assert(boundary is not null, "The caller resolves the hold-owning boundary.");
        Debug.Assert(expired is not null, "The caller supplies the current expired-reservation set.");
        var hard = boundary.Request.OriginalRequest.Limits.FirstOrDefault(limit =>
            limit.Dimension == dimension && limit.Kind == BudgetLimitKind.Hard);
        if (hard is null)
        {
            return [];
        }

        var (reserved, committed) = Usage(boundary, dimension, expired);
        var aggregation = boundary.Reservations
            .First(item => item.Receipt.OriginalRequest.Dimension == dimension).Aggregation;
        var observed = aggregation switch
        {
            BudgetAggregationKind.Maximum => Max(reserved, committed),
            BudgetAggregationKind.ConcurrentGauge => reserved,
            BudgetAggregationKind.Sum or BudgetAggregationKind.Duration => reserved.Add(committed),
            _ => throw new BudgetLedgerStateException("The hold accounting has unsupported aggregation semantics."),
        };
        return observed.CompareTo(BudgetQuantity.FromDecimal(hard.Value)) <= 0
            ? []
            : [new BudgetLimitFailure(
                boundary.Reference.Id, dimension, BudgetLimitKind.Hard, hard.Value, observed, default, hard.Unit,
                "Current accounting exceeds the captured hard boundary.")];
    }

    private static bool IsParentAddress(BudgetScopeAddress parent, BudgetScopeAddress child)
    {
        Debug.Assert(parent is not null, "The caller resolves a persisted parent address.");
        Debug.Assert(child is not null, "The validated scope request supplies a child address.");
        return parent.TenantId == child.TenantId
            && parent.PrincipalId == child.PrincipalId
            && parent.AgentId == child.AgentId
            && (parent.SessionId is null || parent.SessionId == child.SessionId)
            && (parent.RunId is null || parent.RunId == child.RunId)
            && (parent.OperationId is null || parent.OperationId == child.OperationId);
    }

    private static BudgetLedgerMutationConflictException Conflict(string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "The caller supplies a content-free conflict message.");
        return new BudgetLedgerMutationConflictException(message);
    }

    private static BudgetLedgerReferenceUnavailableException Missing(string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "The caller supplies a content-free unavailable-reference message.");
        return new BudgetLedgerReferenceUnavailableException(message);
    }

    private static BudgetLedgerPersistenceUnavailableException Unavailable(
        string message, bool acknowledgementUnknown, Exception? inner = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "The caller supplies a content-free persistence message.");
        return inner is null
            ? new BudgetLedgerPersistenceUnavailableException(message, acknowledgementUnknown)
            : new BudgetLedgerPersistenceUnavailableException(message, acknowledgementUnknown, inner);
    }
}
