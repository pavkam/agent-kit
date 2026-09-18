// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Persists approval requests and their single terminal response as a newline-delimited JSON transition log.</summary>
/// <remarks>
/// <para>
/// The store is log structured. Every state change is one JSON line appended and flushed to disk before the operation is
/// acknowledged, so an acknowledged creation or resolution survives process loss. A pending request that outlives the
/// process is exactly what makes deferred human approval usable, and a recorded decision is never replayed as pending.
/// </para>
/// <para>
/// Live state is projected into memory during <see cref="InitializeAsync"/> and kept authoritative under one in-process
/// gate, so two concurrent responders cannot both resolve the same request. A host-local advisory exclusive lock is held for
/// the store's lifetime, so a second writer on the same host fails fast instead of interleaving appends. This is durable
/// single-process host-local storage: it provides no distributed lease, no fencing token, and no atomicity with any external
/// effect such as notifying the approver.
/// </para>
/// <para>
/// Call <see cref="InitializeAsync"/> exactly once during trusted bootstrap before resolving the store for use. Requests and
/// responses are retained indefinitely, because a terminal approval decision is audit evidence and an idempotency key long
/// after the operation it authorized has finished.
/// </para>
/// </remarks>
public sealed partial class JsonApprovalStore: IApprovalStore, IDisposable
{
    private const string _storeKind = "agentkit.permissions.approvals";
    private const string _logName = "approvals";
    private const int _schemaVersion = 1;
    private const string _failureKindKey = "agentkit.failure_kind";
    private const string _openFailed = "open_failed";
    private const string _corruptEvidence = "corrupt_evidence";
    private const string _schemaUnsupported = "schema_unsupported";
    private const string _persistenceFailed = "persistence_failed";
    private readonly JsonApprovalStoreTarget _target;
    private readonly JsonApprovalStoreSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonApprovalStore> _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private readonly Lock _gate = new();
    private readonly Dictionary<ApprovalRequestId, ApprovalEntry> _entries = [];
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a store for one host-authorized fixed root without opening, creating, or locking it.</summary>
    /// <param name="target">The exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable evidence bounds, compaction policy, and encoding contract.</param>
    /// <param name="timeProvider">The clock used only to measure operation duration for diagnostics; approval instants are supplied by the caller and are never fabricated here.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    /// <remarks>Construction performs no I/O, so composition never touches the filesystem; every effect happens in <see cref="InitializeAsync"/>.</remarks>
    public JsonApprovalStore(
        JsonApprovalStoreTarget target,
        JsonApprovalStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<JsonApprovalStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<JsonApprovalStore>.Instance;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <inheritdoc/>
    /// <value>
    /// Durable, trusted-control-plane capabilities. Durability is claimed because every acknowledged transition was flushed
    /// to local disk before the call returned; it is host-local durability and makes no distributed or replicated claim.
    /// </value>
    public ApprovalStoreCapabilities Capabilities { get; } = new(
        IsDurable: true,
        ProvidesTrustedControlPlane: true);

    /// <summary>Validates or creates the store root, binds its encoding contract, and replays live state into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for approval operations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, store identity, encoding contract, or persisted evidence cannot be validated safely.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
    /// <remarks>
    /// <para>
    /// Initialization acquires the advisory exclusive lock first, so a concurrent writer is rejected before any validation
    /// observes a racing state. It then verifies the manifest's store identity, schema version, and encoding fingerprint,
    /// and performs a round-trip self-check proving the configured contract can reproduce this store's evidence.
    /// </para>
    /// <para>
    /// A log ending in an incomplete append is discarded only under <see cref="JsonStoreRecoveryMode.RecoverTornAppends"/>;
    /// under <see cref="JsonStoreRecoveryMode.ValidateExact"/> it is reported as corrupt evidence. Repeating initialization
    /// after success is a no-op.
    /// </para>
    /// </remarks>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ExecuteVoidAsync(
        "initialize",
        () =>
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
                JsonStoreSerialization.VerifyRoundTrip(
                    JsonApprovalStoreProbe.Create(), _settings.Encoding.RecordOptions);
                cancellationToken.ThrowIfCancellationRequested();
                BindManifest(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Replay(cancellationToken);
                _initialized = true;
            }
        });

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the creation append begins.</exception>
    /// <exception cref="InvalidOperationException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
    /// <remarks>
    /// Creating the exact same immutable request again is idempotent and appends nothing, and a different request under an
    /// existing identity is a conflict that leaves the log untouched. Because the record is flushed before acknowledgement, a
    /// failure reported to the caller means the request is either absent or byte-identical to the retried evidence.
    /// </remarks>
    public ValueTask<ApprovalStoreCreateResult> CreateAsync(
        ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync("create", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                RequireInitialized();
                if (_entries.TryGetValue(request.Id, out var existing))
                {
                    return existing.Request == request
                        ? ApprovalStoreCreateResult.AlreadyExists
                        : ApprovalStoreCreateResult.Conflict;
                }

                cancellationToken.ThrowIfCancellationRequested();
                AppendRecord(JsonApprovalLogRecord.ForCreation(request), cancellationToken);
                _entries.Add(request.Id, new ApprovalEntry(request, null));
                return ApprovalStoreCreateResult.Created;
            }
        }, request.Id, null, request.Binding);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the resolution append begins.</exception>
    /// <exception cref="InvalidOperationException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
    /// <remarks>
    /// A resolution is terminal. Replaying the identical response is idempotent and appends nothing, a different response for
    /// an already resolved request is a conflict, and a response whose binding differs from the request it claims to resolve
    /// is a conflict rather than an update, so a decision can never be retargeted at a wider scope than the approver saw.
    /// </remarks>
    public ValueTask<ApprovalStoreResolveResult> ResolveAsync(
        ApprovalResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ExecuteAsync("resolve", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                RequireInitialized();
                if (!_entries.TryGetValue(response.RequestId, out var existing))
                {
                    return ApprovalStoreResolveResult.NotFound;
                }
                if (existing.Response is { } terminal)
                {
                    return terminal == response
                        ? ApprovalStoreResolveResult.AlreadyResolved
                        : ApprovalStoreResolveResult.Conflict;
                }
                if (existing.Request.Binding != response.Binding)
                {
                    return ApprovalStoreResolveResult.Conflict;
                }

                cancellationToken.ThrowIfCancellationRequested();
                AppendRecord(JsonApprovalLogRecord.ForResolution(response), cancellationToken);
                _entries[response.RequestId] = existing with { Response = response };
                return ApprovalStoreResolveResult.Resolved;
            }
        }, response.RequestId, response.Id, response.Binding);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestId"/> is empty.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the projection is taken.</exception>
    /// <exception cref="InvalidOperationException">The store is uninitialized.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
    /// <remarks>
    /// The read is served from the authoritative in-memory projection of the replayed log and never touches the filesystem,
    /// so it cannot observe a partially written transition. An unknown identity returns an empty result rather than
    /// fabricating a pending request.
    /// </remarks>
    public ValueTask<ApprovalStoreReadResult> ReadAsync(
        ApprovalRequestId requestId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId.Value, Guid.Empty);
        return ExecuteAsync("read", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                RequireInitialized();
                return _entries.TryGetValue(requestId, out var entry)
                    ? new ApprovalStoreReadResult(entry.Request, entry.Response)
                    : new ApprovalStoreReadResult(null, null);
            }
        }, requestId);
    }

    /// <summary>Releases the advisory exclusive lock held for this store's lifetime.</summary>
    /// <remarks>
    /// Disposal is idempotent and does not flush: every acknowledged record was already flushed to disk when it was
    /// appended. Projected state is dropped, so a disposed store cannot serve further operations.
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
            _entries.Clear();
            _exclusive?.Dispose();
            _exclusive = null;
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
                throw Unavailable(_openFailed, "The configured JSON approval-store root has no manifest.");
            }

            var created = new JsonStoreManifest(_target.ExpectedStoreInstanceId.Value, _storeKind, _schemaVersion,
                _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(_root.ManifestPath,
                JsonStoreSerialization.Encode(created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        if (manifest.StoreId != _target.ExpectedStoreInstanceId.Value
            || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw Unavailable(_corruptEvidence,
                "The JSON approval-store identity does not match bootstrap configuration.");
        }
        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable(_schemaUnsupported, "The JSON approval-store schema version is unsupported.");
        }
        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable(_schemaUnsupported,
                "The JSON approval-store root was written under a different encoding contract.");
        }
    }

    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_entries.Count == 0, "Replay populates an empty projection.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable(_corruptEvidence, "The JSON approval-store log ends with an incomplete record.");
        }

        foreach (var record in replay.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Apply(JsonStoreSerialization.Decode<JsonApprovalLogRecord>(
                record.Span, _settings.Encoding.RecordOptions));
        }

        if (replay.HasIncompleteTrailingRecord || replay.Records.Count > _settings.CompactionRecordThreshold)
        {
            Compact(cancellationToken);
        }
    }

    private void Apply(JsonApprovalLogRecord record)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        switch (record.Kind)
        {
            case JsonApprovalLogRecordKind.Created:
                {
                    var request = Require(record.Request, "creation").ToDomain();
                    if (_entries.ContainsKey(request.Id))
                    {
                        throw Unavailable(_corruptEvidence,
                            "A persisted approval identity carries more than one creation record.");
                    }

                    _entries.Add(request.Id, new ApprovalEntry(request, null));
                    return;
                }
            case JsonApprovalLogRecordKind.Resolved:
                {
                    var response = Require(record.Response, "resolution").ToDomain();
                    if (!_entries.TryGetValue(response.RequestId, out var entry))
                    {
                        throw Unavailable(_corruptEvidence,
                            "A persisted approval resolution references an unknown request.");
                    }
                    if (entry.Response is not null)
                    {
                        throw Unavailable(_corruptEvidence,
                            "A persisted approval request carries more than one terminal resolution.");
                    }
                    if (entry.Request.Binding != response.Binding)
                    {
                        throw Unavailable(_corruptEvidence,
                            "A persisted approval resolution does not match its request binding.");
                    }

                    _entries[response.RequestId] = entry with { Response = response };
                    return;
                }
            default:
                throw Unavailable(_corruptEvidence,
                    "A persisted approval-store record has an unsupported kind.");
        }
    }

    private void Compact(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "Compaction runs only while the exclusive lock is held.");
        var records = new List<byte[]>(_entries.Count);
        foreach (var entry in _entries.Values)
        {
            records.Add(EncodeRecord(JsonApprovalLogRecord.ForCreation(entry.Request)));
            if (entry.Response is { } response)
            {
                records.Add(EncodeRecord(JsonApprovalLogRecord.ForResolution(response)));
            }
        }

        _log.Compact(records, cancellationToken);
    }

    private void AppendRecord(JsonApprovalLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A record is required.");
        _log.Append(EncodeRecord(record), cancellationToken);
    }

    private byte[] EncodeRecord(JsonApprovalLogRecord record)
    {
        Debug.Assert(record is not null, "A record is required.");
        return JsonStoreSerialization.Encode(record, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
    }

    private void RequireInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw Unavailable(_openFailed,
                "The JSON approval store was used before trusted bootstrap initialization.");
        }
    }

    private static T Require<T>(T? value, string context)
        where T : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value ?? throw Unavailable(_corruptEvidence,
            $"A persisted approval {context} record omits its required payload.");
    }

    /// <summary>Builds the terminal failure for evidence this store refuses to serve.</summary>
    /// <param name="failureKind">The bounded failure classification attached for observation.</param>
    /// <param name="message">The bounded content-free failure message.</param>
    /// <param name="inner">The originating exception, when one exists.</param>
    /// <returns>An exception whose <see cref="Exception.Data"/> carries the bounded classification.</returns>
    /// <remarks>
    /// The approval contract defines no dedicated unavailability exception, so the store fails closed with a plain
    /// <see cref="InvalidOperationException"/> rather than introducing a package-local exception type that callers of
    /// <see cref="IApprovalStore"/> could not be expected to catch. The bounded failure class travels in
    /// <see cref="Exception.Data"/> under a stable key so diagnostics can report it without parsing the message and without
    /// changing the exception type callers observe.
    /// </remarks>
    private static InvalidOperationException Unavailable(
        string failureKind, string message, Exception? inner = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(failureKind), "A bounded failure classification is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        var exception = new InvalidOperationException(message, inner);
        exception.Data[_failureKindKey] = failureKind;
        return exception;
    }

    private sealed record ApprovalEntry(ApprovalRequest Request, ApprovalResponse? Response);
}
