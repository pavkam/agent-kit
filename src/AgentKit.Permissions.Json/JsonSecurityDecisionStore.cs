// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Persists terminal security decisions as a newline-delimited JSON append-only log.</summary>
/// <remarks>
/// Every acknowledged record is flushed before the call returns. Live history is projected during
/// <see cref="InitializeAsync"/> and updated under one in-process gate. Call <see cref="InitializeAsync"/> once during
/// trusted bootstrap before use.
/// </remarks>
public sealed partial class JsonSecurityDecisionStore: ISecurityDecisionStore, IDisposable
{
    private const string _storeKind = "agentkit.permissions.decisions";
    private const string _logName = "decisions";
    private const int _schemaVersion = 1;
    private const string _failureKindKey = "agentkit.failure_kind";
    private const string _openFailed = "open_failed";
    private const string _corruptEvidence = "corrupt_evidence";
    private const string _schemaUnsupported = "schema_unsupported";
    private const string _persistenceFailed = "persistence_failed";
    private readonly JsonSecurityDecisionStoreTarget _target;
    private readonly JsonSecurityDecisionStoreSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonSecurityDecisionStore> _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private readonly Lock _gate = new();
    private readonly List<SecurityDecision> _decisions = [];
    private JsonStoreLock? _exclusive;
    private bool _initialized;
    private bool _disposed;

    /// <summary>Initializes a store for one host-authorized fixed root without opening, creating, or locking it.</summary>
    /// <param name="target">The exact store root and bootstrap effects supplied by the host.</param>
    /// <param name="settings">The immutable evidence bounds, compaction policy, and encoding contract.</param>
    /// <param name="timeProvider">The clock used only to measure operation duration for diagnostics.</param>
    /// <param name="logger">The optional content-free diagnostic logger.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public JsonSecurityDecisionStore(
        JsonSecurityDecisionStoreTarget target,
        JsonSecurityDecisionStoreSettings settings,
        TimeProvider timeProvider,
        ILogger<JsonSecurityDecisionStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _target = target;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<JsonSecurityDecisionStore>.Instance;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <summary>Gets every recorded decision in arrival order.</summary>
    /// <value>An immutable snapshot of decisions retained so far.</value>
    public IReadOnlyList<SecurityDecision> Decisions
    {
        get
        {
            lock (_gate)
            {
                return _decisions.ToArray();
            }
        }
    }

    /// <summary>Validates or creates the store root, binds its encoding contract, and replays recorded decisions into memory.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>A task completed after the exact root is locked, validated, and ready for recording.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before initialization completes.</exception>
    /// <exception cref="InvalidOperationException">The root, manifest, store identity, encoding contract, or persisted evidence cannot be validated safely.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
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
                    JsonSecurityDecisionStoreProbe.Create(), _settings.Encoding.RecordOptions);
                cancellationToken.ThrowIfCancellationRequested();
                BindManifest(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Replay(cancellationToken);
                _initialized = true;
            }
        });

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the append commits.</exception>
    /// <exception cref="InvalidOperationException">The store is uninitialized, or the append cannot be completed and flushed.</exception>
    /// <exception cref="ObjectDisposedException">The store was already disposed.</exception>
    public ValueTask RecordAsync(SecurityDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return ExecuteVoidAsync("record", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                RequireInitialized();
                cancellationToken.ThrowIfCancellationRequested();
                AppendRecord(JsonSecurityDecisionLogRecord.ForRecorded(decision), cancellationToken);
                _decisions.Add(decision);
            }
        }, decision.RequestId);
    }

    /// <summary>Releases the advisory exclusive lock held for this store's lifetime.</summary>
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
            _decisions.Clear();
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
                throw Unavailable(_openFailed, "The configured JSON decision-store root has no manifest.");
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
                "The JSON decision-store identity does not match bootstrap configuration.");
        }
        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable(_schemaUnsupported, "The JSON decision-store schema version is unsupported.");
        }
        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable(_schemaUnsupported,
                "The JSON decision-store root was written under a different encoding contract.");
        }
    }

    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_decisions.Count == 0, "Replay populates an empty projection.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable(_corruptEvidence, "The JSON decision-store log ends with an incomplete record.");
        }

        foreach (var record in replay.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Apply(JsonStoreSerialization.Decode<JsonSecurityDecisionLogRecord>(
                record.Span, _settings.Encoding.RecordOptions));
        }

        if (replay.HasIncompleteTrailingRecord || replay.Records.Count > _settings.CompactionRecordThreshold)
        {
            Compact(cancellationToken);
        }
    }

    private void Apply(JsonSecurityDecisionLogRecord record)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        if (record.Kind != JsonSecurityDecisionLogRecordKind.Recorded)
        {
            throw Unavailable(_corruptEvidence, "A persisted decision-store record has an unsupported kind.");
        }

        var decision = Require(record.Decision, "recorded").ToDomain();
        _decisions.Add(decision);
    }

    private void Compact(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "Compaction runs only while the exclusive lock is held.");
        var records = new List<byte[]>(_decisions.Count);
        foreach (var decision in _decisions)
        {
            records.Add(EncodeRecord(JsonSecurityDecisionLogRecord.ForRecorded(decision)));
        }

        _log.Compact(records, cancellationToken);
    }

    private void AppendRecord(JsonSecurityDecisionLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A record is required.");
        _log.Append(EncodeRecord(record), cancellationToken);
    }

    private byte[] EncodeRecord(JsonSecurityDecisionLogRecord record)
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
                "The JSON security decision store was used before trusted bootstrap initialization.");
        }
    }

    private static JsonSecurityDecision Require(JsonSecurityDecision? value, string context) =>
        value ?? throw Unavailable(_corruptEvidence,
            $"A persisted decision {context} record omits its required payload.");

    private static InvalidOperationException Unavailable(
        string failureKind, string message, Exception? inner = null)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(failureKind), "A bounded failure classification is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        var exception = new InvalidOperationException(message, inner);
        exception.Data[_failureKindKey] = failureKind;
        return exception;
    }
}
