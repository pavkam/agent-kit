// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Records durable operation evidence as a newline-delimited JSON append-only log under one fixed local root.</summary>
/// <remarks>
/// <para>
/// Every acknowledged write is flushed to disk before the call returns, and the log retains transitions rather than
/// rewritten state, so an acknowledged record survives process loss. Live state is projected during trusted bootstrap
/// initialization and maintained under one in-process gate; a crashed process's incomplete trailing append is
/// recovered or refused according to the target's explicit recovery mode.
/// </para>
/// <para>
/// Journal access is protected. Each write consumes its single-use grant and completes required audit before anything
/// is appended, and an authorized evidence read is unfenced so a recovering worker can learn what happened before it
/// seeks ownership. The journal fails closed: an unavailable grant store or audit dispatcher denies the operation
/// instead of writing unaudited state.
/// </para>
/// <para>
/// This adapter holds an advisory exclusive lock on its root and rejects a second writer, so it claims no
/// multi-process coordination and ships no lease manager. Fencing tokens are still enforced against the persisted
/// last-writer generation, which protects a restarted single writer from a stale generation; it does not make two
/// concurrent hosts safe. A composition that needs cross-process ownership selects the SQLite adapter, whose lease
/// manager allocates generations inside the shared database.
/// </para>
/// </remarks>
public sealed class JsonDurableOperationJournal: IDurableOperationJournal, IDisposable
{
    private const string _storeKind = "agentkit.durability.operations";
    private const string _logName = "operations";
    private const int _schemaVersion = 1;
    private const string _failureKindKey = "agentkit.failure_kind";
    private const string _openFailed = "open_failed";
    private const string _corruptEvidence = "corrupt_evidence";
    private const string _schemaUnsupported = "schema_unsupported";

    private readonly JsonDurableStoreTarget _target;
    private readonly JsonDurableStoreSettings _settings;
    private readonly DurableJournalEnforcement _enforcement;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonDurableOperationJournal> _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private readonly Lock _gate = new();
    private readonly Dictionary<DurableOperationAddress, DurableOperationProjection> _operations = [];
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a journal bound to one selected key and one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="key">The exact registration key every authorized request must target.</param>
    /// <param name="target">The non-null exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The non-null immutable evidence bounds, compaction policy, and encoding contract.</param>
    /// <param name="auditRecordIds">The non-null generator for each required audit record's stable identity.</param>
    /// <param name="auditDispatcher">The non-null required audit dispatcher that must accept every consumed access intent.</param>
    /// <param name="grants">The non-null authoritative store that validates and consumes each exact single-use journal grant.</param>
    /// <param name="timeProvider">The non-null injected clock used for commit timestamps and elapsed measurement.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text.</exception>
    /// <remarks>The security dependencies are required at construction rather than per call, so a composition cannot produce a journal that silently skips grant consumption or audit.</remarks>
    public JsonDurableOperationJournal(
        DurableJournalKey key,
        JsonDurableStoreTarget target,
        JsonDurableStoreSettings settings,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider,
        ILogger<JsonDurableOperationJournal>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Key = key;
        _target = target;
        _settings = settings;
        _enforcement = new DurableJournalEnforcement(
            key, SecurityAudience, auditRecordIds, auditDispatcher, grants, timeProvider);
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<JsonDurableOperationJournal>.Instance;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <summary>Gets the registration key this journal instance answers for.</summary>
    /// <value>The nonblank key an <see cref="AuthorizedDurableRequest{TRequest}"/> must name to reach this journal.</value>
    public DurableJournalKey Key { get; }

    /// <inheritdoc/>
    /// <value>The stable component identity of this adapter. A grant minted for another audience is refused.</value>
    public ComponentId SecurityAudience { get; } = new("agentkit.durability.json");

    /// <summary>Validates or creates the store root, binds its encoding contract, and replays recorded transitions into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for journal operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, store identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer already holds the advisory lock.</exception>
    /// <exception cref="ObjectDisposedException">The journal was already disposed.</exception>
    /// <remarks>Call this once during trusted host startup; repeating it after the first success is a no-op.</remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
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
            JsonStoreSerialization.VerifyRoundTrip(DurableJournalProbe.Create(), _settings.Encoding.RecordOptions);
            cancellationToken.ThrowIfCancellationRequested();
            BindManifest(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Replay(cancellationToken);
            _initialized = true;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        var request = start.Request;
        var binding = request.Descriptor.Binding;
        return WriteAsync(
            DurableJournalWriteOperation.RecordStart,
            start,
            binding.Address,
            binding.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Create,
            receipt => CommitStart(request, receipt, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var request = checkpoint.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordCheckpoint,
            checkpoint,
            request.Address,
            request.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Append,
            receipt => CommitCheckpoint(request, receipt, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        var request = result.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordTerminal,
            result,
            request.Address,
            request.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Append,
            receipt => CommitTerminal(request, receipt, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        var request = waiting.Request;
        return WriteAsync(
            DurableJournalWriteOperation.RecordWaiting,
            waiting,
            request.Address,
            request.ExecutionContext.Authorization,
            request.FencingToken,
            DurableJournalSecurityBinding.Fingerprint(request),
            SecurityEffect.Mutate,
            receipt => CommitWaiting(request, receipt, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The read is authorized and audited but unfenced: a recovering worker must be able to learn what happened before
    /// it seeks ownership, so presenting a fence it has not acquired is refused rather than required. A bare address
    /// carries no captured context, so authorization comes from the presented grant's own capture, which must describe
    /// the address exactly.
    /// </remarks>
    public async ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        var operationAddress = address.Request;
        var started = DurableStorageDiagnostics.TryGetTimestamp(_timeProvider);
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalLoadEvidence,
            ActivityKind.Internal,
            CreateTags(operationAddress));
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireInitialized();
            var (_, denial) = await _enforcement.EnforceAsync(
                address,
                operationAddress,
                address.Grant.Authorization,
                requiredFence: null,
                DurableJournalSecurityBinding.Fingerprint(operationAddress),
                SecurityOperationKind.StateRead,
                SecurityEffect.Observe,
                cancellationToken).ConfigureAwait(false);
            if (denial is not null)
            {
                FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Denied, started, null);
                return new RecoveryEvidenceUnavailable(denial);
            }

            cancellationToken.ThrowIfCancellationRequested();
            DurableOperationProjection? existing;
            lock (_gate)
            {
                RequireInitialized();
                _ = _operations.TryGetValue(operationAddress, out existing);
            }

            if (existing is null)
            {
                FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.NotFound, started, null);
                return new RecoveryEvidenceNotFound(operationAddress);
            }

            FinishEvidenceLoad(activity, DurableJournalEvidenceOutcome.Loaded, started, null);
            return new RecoveryEvidenceLoaded(existing.ToEvidence());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishEvidenceLoad(
                activity, DurableJournalEvidenceOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
    }

    /// <summary>Releases the advisory exclusive lock held for this journal's lifetime.</summary>
    /// <remarks>Disposal drops projected state only; every acknowledged record is already flushed to the log and is replayed by the next initialization.</remarks>
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
            _operations.Clear();
            _exclusive?.Dispose();
            _exclusive = null;
        }
    }

    private DurableRecordResult CommitStart(
        DurableOperationStart start,
        DurableJournalEnforcementReceipt receipt,
        CancellationToken cancellationToken)
    {
        Debug.Assert(start is not null, "An acceptance declaration is required.");
        var address = start.Descriptor.Binding.Address;
        lock (_gate)
        {
            RequireInitialized();
            _ = _operations.TryGetValue(address, out var existing);
            if (DurableJournalTransition.RejectStart(existing, start) is { } rejection)
            {
                return rejection;
            }

            // Read the clock before appending: if GetUtcNow() throws after the record is flushed, the caller would
            // see an exception for a record the journal had already committed, bypassing the
            // DurableRecordFailed(committed: …) channel meant to report exactly that.
            var recordedAt = _timeProvider.GetUtcNow();
            Append(DurableJournalRecord.ForStarted(start), cancellationToken);
            _operations[address] = DurableJournalTransition.ApplyStart(existing, start);
            return new DurableRecorded(start.FencingToken, recordedAt, receipt);
        }
    }

    private DurableRecordResult CommitCheckpoint(
        DurableCheckpoint checkpoint,
        DurableJournalEnforcementReceipt receipt,
        CancellationToken cancellationToken)
    {
        Debug.Assert(checkpoint is not null, "A checkpoint is required.");
        lock (_gate)
        {
            RequireInitialized();
            _ = _operations.TryGetValue(checkpoint.Address, out var existing);
            if (DurableJournalTransition.RejectCheckpoint(existing, checkpoint) is { } rejection)
            {
                return rejection;
            }

            Debug.Assert(existing is not null, "A permitted checkpoint always targets an existing projection.");

            // Read the clock before appending; see the identical comment in CommitStart.
            var recordedAt = _timeProvider.GetUtcNow();
            Append(DurableJournalRecord.ForCheckpointed(checkpoint), cancellationToken);
            DurableJournalTransition.ApplyCheckpoint(existing, checkpoint);
            return new DurableRecorded(checkpoint.FencingToken, recordedAt, receipt);
        }
    }

    private DurableRecordResult CommitTerminal(
        DurableOperationResult terminal,
        DurableJournalEnforcementReceipt receipt,
        CancellationToken cancellationToken)
    {
        Debug.Assert(terminal is not null, "A terminal result is required.");
        lock (_gate)
        {
            RequireInitialized();
            _ = _operations.TryGetValue(terminal.Address, out var existing);
            if (DurableJournalTransition.RejectTerminal(existing, terminal) is { } rejection)
            {
                return rejection;
            }

            Debug.Assert(existing is not null, "A permitted terminal record always targets an existing projection.");
            if (DurableJournalTransition.RepeatsTerminal(existing, terminal))
            {
                return new DurableRecorded(terminal.FencingToken, _timeProvider.GetUtcNow(), receipt);
            }

            // Read the clock before appending; see the identical comment in CommitStart.
            var recordedAt = _timeProvider.GetUtcNow();
            Append(DurableJournalRecord.ForSettled(terminal), cancellationToken);
            DurableJournalTransition.ApplyTerminal(existing, terminal);
            return new DurableRecorded(terminal.FencingToken, recordedAt, receipt);
        }
    }

    private DurableRecordResult CommitWaiting(
        DurableOperationWaiting waiting,
        DurableJournalEnforcementReceipt receipt,
        CancellationToken cancellationToken)
    {
        Debug.Assert(waiting is not null, "A waiting record is required.");
        lock (_gate)
        {
            RequireInitialized();
            _ = _operations.TryGetValue(waiting.Address, out var existing);
            if (DurableJournalTransition.RejectWaiting(existing, waiting) is { } rejection)
            {
                return rejection;
            }

            Debug.Assert(existing is not null, "A permitted waiting record always targets an existing projection.");

            // Read the clock before appending; see the identical comment in CommitStart.
            var recordedAt = _timeProvider.GetUtcNow();
            Append(DurableJournalRecord.ForWaiting(waiting), cancellationToken);
            DurableJournalTransition.ApplyWaiting(existing, waiting);
            return new DurableRecorded(waiting.FencingToken, recordedAt, receipt);
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
                throw Unavailable(_openFailed, "The configured JSON durable-journal root has no manifest.");
            }

            var created = new JsonStoreManifest(
                _target.ExpectedStoreInstanceId.Value, _storeKind, _schemaVersion, _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(
                _root.ManifestPath,
                JsonStoreSerialization.Encode(
                    created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        if (manifest.StoreId != _target.ExpectedStoreInstanceId.Value
            || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw Unavailable(
                _corruptEvidence, "The JSON durable-journal identity does not match bootstrap configuration.");
        }

        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable(_schemaUnsupported, "The JSON durable-journal schema version is unsupported.");
        }

        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable(
                _schemaUnsupported, "The JSON durable-journal root was written under a different encoding contract.");
        }
    }

    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_operations.Count == 0, "Replay populates an empty projection.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable(_corruptEvidence, "The JSON durable-journal log ends with an incomplete record.");
        }

        foreach (var record in replay.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Apply(JsonStoreSerialization.Decode<DurableJournalRecord>(record.Span, _settings.Encoding.RecordOptions));
        }

        if (replay.HasIncompleteTrailingRecord)
        {
            DurableStorageDiagnostics.SafeObserve(() => JsonDurableJournalLog.RecoveredTornAppend(_logger));
        }

        if (replay.HasIncompleteTrailingRecord || replay.Records.Count > _settings.CompactionRecordThreshold)
        {
            Compact(replay.Records.Count, cancellationToken);
        }
    }

    private void Apply(DurableJournalRecord record)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        switch (record.Kind)
        {
            case DurableJournalRecordKind.Started:
                ApplyStarted(Require(record.Started, "started").ToDomain());
                return;

            case DurableJournalRecordKind.Checkpointed:
                ApplyCheckpointed(Require(record.Checkpointed, "checkpointed").ToDomain());
                return;

            case DurableJournalRecordKind.Settled:
                ApplySettled(Require(record.Settled, "settled").ToDomain());
                return;

            case DurableJournalRecordKind.Waiting:
                ApplyWaiting(Require(record.Waiting, "waiting").ToDomain());
                return;

            case DurableJournalRecordKind.Snapshot:
                ApplySnapshot(Require(record.Snapshot, "snapshot").ToDomain());
                return;

            default:
                throw Unavailable(_corruptEvidence, "A persisted durable-journal record has an unsupported kind.");
        }
    }

    private void ApplyStarted(DurableOperationStart start)
    {
        Debug.Assert(start is not null, "A decoded acceptance declaration is required.");
        var address = start.Descriptor.Binding.Address;
        _ = _operations.TryGetValue(address, out var existing);
        _operations[address] = DurableJournalTransition.ApplyStart(existing, start);
    }

    private void ApplyCheckpointed(DurableCheckpoint checkpoint)
    {
        Debug.Assert(checkpoint is not null, "A decoded checkpoint is required.");
        DurableJournalTransition.ApplyCheckpoint(RequireOperation(checkpoint.Address), checkpoint);
    }

    private void ApplySettled(DurableOperationResult terminal)
    {
        Debug.Assert(terminal is not null, "A decoded terminal result is required.");
        DurableJournalTransition.ApplyTerminal(RequireOperation(terminal.Address), terminal);
    }

    private void ApplyWaiting(DurableOperationWaiting waiting)
    {
        Debug.Assert(waiting is not null, "A decoded waiting record is required.");
        DurableJournalTransition.ApplyWaiting(RequireOperation(waiting.Address), waiting);
    }

    private void ApplySnapshot(DurableOperationProjection projection)
    {
        Debug.Assert(projection is not null, "A decoded projection is required.");
        _operations[projection.Binding.Address] = projection;
    }

    private void Compact(int replayedRecords, CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "Compaction runs only while the exclusive lock is held.");
        var records = new List<byte[]>(_operations.Count);
        foreach (var projection in _operations.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(Encode(DurableJournalRecord.ForSnapshot(projection)));
        }

        _log.Compact(records, cancellationToken);
        var liveOperations = records.Count;
        DurableStorageDiagnostics.SafeObserve(
            () => JsonDurableJournalLog.Compacted(_logger, replayedRecords, liveOperations));
    }

    private void Append(DurableJournalRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A record is required.");
        _log.Append(Encode(record), cancellationToken);
    }

    private byte[] Encode(DurableJournalRecord record)
    {
        Debug.Assert(record is not null, "A record is required.");
        return JsonStoreSerialization.Encode(record, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
    }

    private DurableOperationProjection RequireOperation(DurableOperationAddress address) =>
        _operations.TryGetValue(address, out var existing)
            ? existing
            : throw Unavailable(
                _corruptEvidence, "A persisted durable-journal transition targets an operation that was never started.");

    private void RequireInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw Unavailable(
                _openFailed, "The JSON durable operation journal was used before trusted bootstrap initialization.");
        }
    }

    /// <summary>Authorizes, audits, and then runs one write body under the journal gate with shared diagnostics.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="operation">The bounded write-stage dimension.</param>
    /// <param name="request">The protected request carrying the grant and enforcement intent.</param>
    /// <param name="address">The operation coordinates the write targets.</param>
    /// <param name="authorization">The capture the write must run under.</param>
    /// <param name="fencingToken">The ownership generation the write presents; it is also the required intent fence.</param>
    /// <param name="fingerprint">The canonical digest over the complete request.</param>
    /// <param name="effect">The protected effect this write performs.</param>
    /// <param name="body">The commit body executed after authorization succeeds.</param>
    /// <param name="cancellationToken">Cancels the attempt before it commits.</param>
    /// <returns>The write body's terminal result, or a non-committed failure when authorization denied it.</returns>
    /// <remarks>
    /// Authorization strictly precedes the append, so a denial can never leave partial state. A file-system failure
    /// becomes a failure result with unknown commit status rather than an exception, because an append that failed
    /// midway may still have reached the disk, and a caller that cannot tell must not be told it definitely did not.
    /// Diagnostics are observational.
    /// </remarks>
    private async ValueTask<DurableRecordResult> WriteAsync<TRequest>(
        DurableJournalWriteOperation operation,
        AuthorizedDurableRequest<TRequest> request,
        DurableOperationAddress address,
        SecurityAuthorizationContext authorization,
        FencingToken fencingToken,
        InputFingerprint fingerprint,
        SecurityEffect effect,
        Func<DurableJournalEnforcementReceipt, DurableRecordResult> body,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        Debug.Assert(body is not null, "A write body is required.");
        var started = DurableStorageDiagnostics.TryGetTimestamp(_timeProvider);
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DurableJournalWrite,
            ActivityKind.Internal,
            new ActivityTagsCollection { { AgentKitTagNames.DurableJournalOperation, operation.ToStableValue() } });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireInitialized();
            var (receipt, denial) = await _enforcement.EnforceAsync(
                request,
                address,
                authorization,
                fencingToken,
                fingerprint,
                SecurityOperationKind.StateMutation,
                effect,
                cancellationToken).ConfigureAwait(false);
            if (receipt is not { } enforcement)
            {
                var refused = new DurableRecordFailed(
                    denial ?? "The durable journal operation was not authorized.", committed: false);
                FinishWrite(activity, operation, DurableJournalWriteOutcome.Denied, started, null);
                return refused;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = body(enforcement);
            FinishWrite(activity, operation, Classify(result), started, null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishWrite(
                activity, operation, DurableJournalWriteOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
        catch (IOException exception)
        {
            FinishWrite(
                activity, operation, DurableJournalWriteOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception));
            return new DurableRecordFailed(
                "The JSON durable journal could not complete the write.", committed: null);
        }
        catch (UnauthorizedAccessException exception)
        {
            FinishWrite(
                activity, operation, DurableJournalWriteOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception));
            return new DurableRecordFailed(
                "The JSON durable journal could not complete the write.", committed: null);
        }
        catch (Exception exception)
        {
            FinishWrite(
                activity, operation, DurableJournalWriteOutcome.Failed, started,
                DurableStorageDiagnostics.ErrorType(exception));
            throw;
        }
    }

    private static ActivityTagsCollection CreateTags(DurableOperationAddress address) => new()
    {
        { AgentKitTagNames.AgentId, address.AgentId.ToString() },
        { AgentKitTagNames.SessionId, address.SessionId.ToString() },
        { AgentKitTagNames.RunId, address.RunId.ToString() },
        { AgentKitTagNames.OperationId, address.OperationId.ToString() },
    };

    private static DurableJournalWriteOutcome Classify(DurableRecordResult result)
    {
        Debug.Assert(result is not null, "A terminal write result is required.");
        return result switch
        {
            DurableRecorded => DurableJournalWriteOutcome.Recorded,
            DurableRecordFenced => DurableJournalWriteOutcome.Fenced,
            _ => DurableJournalWriteOutcome.Failed,
        };
    }

    private void FinishWrite(
        Activity? activity,
        DurableJournalWriteOperation operation,
        DurableJournalWriteOutcome outcome,
        long? started,
        string? errorType)
    {
        var operationValue = operation.ToStableValue();
        var outcomeValue = outcome.ToStableValue();
        DurableStorageDiagnostics.SafeSetActivity(activity, current =>
        {
            if (outcome == DurableJournalWriteOutcome.Recorded)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        DurableStorageDiagnostics.SafeObserve(() =>
        {
            if (errorType is not null && outcome == DurableJournalWriteOutcome.Failed)
            {
                JsonDurableJournalLog.WriteFailed(_logger, operationValue, errorType);
            }
            else if (errorType is not null && outcome == DurableJournalWriteOutcome.Cancelled)
            {
                JsonDurableJournalLog.WriteCancelled(_logger, operationValue);
            }
            else if (outcome == DurableJournalWriteOutcome.Denied)
            {
                JsonDurableJournalLog.WriteDenied(_logger, operationValue);
            }
            else
            {
                JsonDurableJournalLog.WriteCompleted(_logger, operationValue, outcomeValue);
            }
        });
        var elapsed = DurableStorageDiagnostics.TryGetElapsedTime(_timeProvider, started);
        DurableStorageDiagnostics.SafeObserve(() => DurableJournalMetrics.RecordWrite(operation, outcome, elapsed));
    }

    private void FinishEvidenceLoad(
        Activity? activity,
        DurableJournalEvidenceOutcome outcome,
        long? started,
        string? errorType)
    {
        var outcomeValue = outcome.ToStableValue();
        DurableStorageDiagnostics.SafeSetActivity(activity, current =>
        {
            if (outcome is DurableJournalEvidenceOutcome.Loaded or DurableJournalEvidenceOutcome.NotFound)
            {
                current.SetSuccessful(outcomeValue);
            }
            else
            {
                current.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        DurableStorageDiagnostics.SafeObserve(() =>
        {
            if (outcome == DurableJournalEvidenceOutcome.Cancelled)
            {
                JsonDurableJournalLog.EvidenceLoadCancelled(_logger);
            }
            else
            {
                JsonDurableJournalLog.EvidenceLoadCompleted(_logger, outcomeValue);
            }
        });
        var elapsed = DurableStorageDiagnostics.TryGetElapsedTime(_timeProvider, started);
        DurableStorageDiagnostics.SafeObserve(() => DurableJournalMetrics.RecordEvidenceLoad(outcome, elapsed));
    }

    private static TDocument Require<TDocument>(TDocument? value, string context)
        where TDocument : class =>
        value ?? throw Unavailable(
            _corruptEvidence, $"A persisted durable-journal {context} record omits its required payload.");

    private static InvalidOperationException Unavailable(
        string failureKind,
        string message,
        Exception? inner = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(failureKind), "A bounded failure classification is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        var exception = new InvalidOperationException(message, inner);
        exception.Data[_failureKindKey] = failureKind;
        return exception;
    }
}
