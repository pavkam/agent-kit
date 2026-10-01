// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json;

/// <summary>Owns one locked, manifest-bound JSON root and its flushed newline-delimited result log.</summary>
/// <remarks>
/// The class is not thread-safe: the owning store serializes every call under its own gate. Opening validates the root, takes the
/// advisory exclusive lock (a second writer is rejected), proves the encoding contract round-trips, binds the manifest, and replays
/// the log, recovering or refusing a torn trailing append according to the target.
/// </remarks>
internal sealed class JsonEvaluationStoreFile: IDisposable
{
    private const int _schemaVersion = 1;
    private const string _logName = "results";

    private readonly JsonEvaluationStoreTarget _target;
    private readonly JsonEvaluationStoreSettings _settings;
    private readonly string _storeKind;
    private readonly ILogger _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private JsonStoreLock? _exclusive;

    /// <summary>Initializes a file bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="storeKind">The manifest store kind this file must carry.</param>
    /// <param name="logger">The content-free logger.</param>
    internal JsonEvaluationStoreFile(JsonEvaluationStoreTarget target, JsonEvaluationStoreSettings settings, string storeKind, ILogger logger)
    {
        Debug.Assert(target is not null, "The store validates its target before creating the file.");
        Debug.Assert(settings is not null, "The store validates its settings before creating the file.");
        _target = target;
        _settings = settings;
        _storeKind = storeKind;
        _logger = logger;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(_logName), settings.MaximumRecordBytes);
    }

    /// <summary>Validates or creates the root, locks it, binds its manifest, and replays recorded results.</summary>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>The replayed records in append order.</returns>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    internal IReadOnlyList<ReadOnlyMemory<byte>> Open(CancellationToken cancellationToken)
    {
        _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
        JsonStoreRoot.ValidateFile(_root.ManifestPath);
        JsonStoreRoot.ValidateFile(_log.Path);
        _exclusive = JsonStoreLock.Acquire(_root.LockPath);
        try
        {
            Verify(EvaluationResultDocument.FromDomain(EvaluationResultProbe.Create()));
            BindManifest(cancellationToken);
            var replay = _log.Replay(cancellationToken);
            if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
            {
                throw new InvalidOperationException("The JSON evaluation-store log ends with an incomplete record.");
            }

            if (replay.HasIncompleteTrailingRecord)
            {
                EvaluationResultStoreObservation.Safe(() => JsonEvaluationStoreLog.RecoveredTornAppend(_logger));
            }

            var replayed = replay.Records.Count;
            EvaluationResultStoreObservation.Safe(() => JsonEvaluationStoreLog.Replayed(_logger, replayed));
            return replay.Records;
        }
        catch
        {
            _exclusive.Dispose();
            _exclusive = null;
            throw;
        }
    }

    /// <summary>Appends and flushes one encoded record.</summary>
    /// <param name="record">The encoded record.</param>
    /// <param name="cancellationToken">Cancels before the append.</param>
    internal void Append(byte[] record, CancellationToken cancellationToken) => _log.Append(record, cancellationToken);

    /// <summary>Encodes a result with the configured record contract and bound.</summary>
    /// <param name="result">The result to encode.</param>
    /// <returns>The encoded record.</returns>
    /// <exception cref="InvalidDataException">The encoded record exceeds the configured bound.</exception>
    internal byte[] Encode(EvaluationCaseResult result) =>
        EvaluationResultCodec.Encode(result, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);

    /// <summary>Decodes a record with the configured record contract.</summary>
    /// <param name="record">The encoded record.</param>
    /// <returns>The validated result.</returns>
    internal EvaluationCaseResult Decode(ReadOnlySpan<byte> record) =>
        EvaluationResultCodec.Decode(record, _settings.Encoding.RecordOptions);

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    public void Dispose()
    {
        _exclusive?.Dispose();
        _exclusive = null;
    }

    private void Verify(EvaluationResultDocument probe)
    {
        byte[] first;
        byte[] second;
        try
        {
            first = JsonStoreSerialization.Encode(probe, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);
            second = JsonStoreSerialization.Encode(
                JsonStoreSerialization.Decode<EvaluationResultDocument>(first, _settings.Encoding.RecordOptions),
                _settings.Encoding.RecordOptions,
                _settings.MaximumRecordBytes);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException("The configured record bound cannot hold even a minimal evaluation result.", exception);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException("The configured JSON encoding contract cannot round-trip this store's persisted evidence.", exception);
        }

        if (!first.AsSpan().SequenceEqual(second))
        {
            throw new InvalidOperationException("The configured JSON encoding contract does not reproduce this store's persisted evidence exactly.");
        }
    }

    private void BindManifest(CancellationToken cancellationToken)
    {
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw new InvalidOperationException("The configured JSON evaluation-store root has no manifest.");
            }

            var created = new JsonStoreManifest(_target.ExpectedInstanceId.Value, _storeKind, _schemaVersion, _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(
                _root.ManifestPath,
                JsonStoreSerialization.Encode(created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        JsonStoreManifest manifest;
        try
        {
            manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            throw new InvalidOperationException("The JSON evaluation-store manifest cannot be read under the configured encoding contract.", exception);
        }

        if (manifest.StoreId != _target.ExpectedInstanceId.Value || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The JSON evaluation-store identity does not match bootstrap configuration.");
        }

        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw new InvalidOperationException("The JSON evaluation-store schema version is unsupported.");
        }

        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The JSON evaluation-store root was written under a different encoding contract.");
        }
    }
}
