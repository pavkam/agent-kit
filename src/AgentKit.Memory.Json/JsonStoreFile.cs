// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

/// <summary>Owns one locked, manifest-bound JSON root and its flushed newline-delimited record log.</summary>
/// <remarks>
/// The class is not thread-safe: the owning store serializes every call under its own gate. Opening validates the root, takes
/// the advisory exclusive lock (a second writer is rejected), proves the encoding contract round-trips, binds the manifest, and
/// replays the log, recovering or refusing a torn trailing append according to the target.
/// </remarks>
internal sealed class JsonStoreFile: IDisposable
{
    private const int _schemaVersion = 1;

    private readonly JsonMemoryTarget _target;
    private readonly JsonMemorySettings _settings;
    private readonly string _storeKind;
    private readonly string _family;
    private readonly ILogger _logger;
    private readonly JsonStoreRoot _root;
    private readonly JsonRecordLog _log;
    private JsonStoreLock? _exclusive;

    /// <summary>Initializes a file bound to one host-authorized root without opening, creating, or locking it.</summary>
    /// <param name="target">The non-null exact root and bootstrap effects.</param>
    /// <param name="settings">The non-null immutable bounds and encoding contract.</param>
    /// <param name="storeKind">The manifest store kind this file must carry.</param>
    /// <param name="logName">The log file name without extension.</param>
    /// <param name="family">The bounded state family name for logs.</param>
    /// <param name="logger">The content-free logger.</param>
    internal JsonStoreFile(JsonMemoryTarget target, JsonMemorySettings settings, string storeKind, string logName, string family, ILogger logger)
    {
        Debug.Assert(target is not null, "The store validates its target before creating the file.");
        Debug.Assert(settings is not null, "The store validates its settings before creating the file.");
        _target = target;
        _settings = settings;
        _storeKind = storeKind;
        _family = family;
        _logger = logger;
        _root = new JsonStoreRoot(target.DirectoryPath);
        _log = new JsonRecordLog(_root.LogPath(logName), settings.MaximumRecordBytes);
    }

    /// <summary>Gets a value indicating whether the last replay held more records than the compaction threshold or recovered a torn append.</summary>
    internal bool NeedsCompaction { get; private set; }

    /// <summary>Gets the number of records the last replay read.</summary>
    internal int ReplayedRecords { get; private set; }

    /// <summary>Validates or creates the root, locks it, binds its manifest, and replays recorded entries.</summary>
    /// <param name="verifyEncoding">Proves the encoding contract round-trips this store's documents exactly.</param>
    /// <param name="cancellationToken">Cancels before the manifest is written or before replay completes.</param>
    /// <returns>The replayed records in append order.</returns>
    /// <exception cref="InvalidOperationException">The root, manifest, identity, encoding contract, or persisted evidence cannot be validated safely, or a second writer holds the advisory lock.</exception>
    internal IReadOnlyList<ReadOnlyMemory<byte>> Open(Action verifyEncoding, CancellationToken cancellationToken)
    {
        _root.Validate(allowCreate: _target.OpenMode == JsonStoreOpenMode.CreateIfMissing);
        JsonStoreRoot.ValidateFile(_root.ManifestPath);
        JsonStoreRoot.ValidateFile(_log.Path);
        _exclusive = JsonStoreLock.Acquire(_root.LockPath);
        try
        {
            verifyEncoding();
            BindManifest(cancellationToken);
            var replay = _log.Replay(cancellationToken);
            if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
            {
                throw new InvalidOperationException("The JSON memory-store log ends with an incomplete record.");
            }

            if (replay.HasIncompleteTrailingRecord)
            {
                MemoryStoreObservation.Safe(() => JsonMemoryLog.RecoveredTornAppend(_logger, _family));
            }

            ReplayedRecords = replay.Records.Count;
            NeedsCompaction = replay.HasIncompleteTrailingRecord || replay.Records.Count > _settings.CompactionRecordThreshold;
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

    /// <summary>Atomically replaces the log with the given records.</summary>
    /// <param name="records">The encoded snapshot records.</param>
    /// <param name="liveRecords">The number of records kept, for the log.</param>
    /// <param name="cancellationToken">Cancels before the replacement.</param>
    internal void Compact(IReadOnlyList<byte[]> records, int liveRecords, CancellationToken cancellationToken)
    {
        _log.Compact(records, cancellationToken);
        var replayed = ReplayedRecords;
        MemoryStoreObservation.Safe(() => JsonMemoryLog.Compacted(_logger, _family, replayed, liveRecords));
    }

    /// <summary>Encodes a value with the configured record contract and bound.</summary>
    /// <typeparam name="TValue">The document type.</typeparam>
    /// <param name="value">The value to encode.</param>
    /// <returns>The encoded record.</returns>
    internal byte[] Encode<TValue>(TValue value)
        where TValue : notnull =>
        JsonStoreSerialization.Encode(value, _settings.Encoding.RecordOptions, _settings.MaximumRecordBytes);

    /// <summary>Decodes a record with the configured record contract.</summary>
    /// <typeparam name="TValue">The document type.</typeparam>
    /// <param name="record">The encoded record.</param>
    /// <returns>The decoded value.</returns>
    internal TValue Decode<TValue>(ReadOnlySpan<byte> record)
        where TValue : notnull =>
        JsonStoreSerialization.Decode<TValue>(record, _settings.Encoding.RecordOptions);

    /// <summary>Proves the encoding contract round-trips one probe document exactly.</summary>
    /// <typeparam name="TValue">The document type.</typeparam>
    /// <param name="probe">A representative document.</param>
    /// <exception cref="InvalidOperationException">The contract cannot round-trip the probe to identical bytes.</exception>
    internal void Verify<TValue>(TValue probe)
        where TValue : notnull
    {
        var first = Encode(probe);
        byte[] second;
        try
        {
            second = Encode(Decode<TValue>(first));
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

    /// <summary>Releases the advisory lock so another writer may open the root.</summary>
    public void Dispose()
    {
        _exclusive?.Dispose();
        _exclusive = null;
    }

    private void BindManifest(CancellationToken cancellationToken)
    {
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw new InvalidOperationException("The configured JSON memory-store root has no manifest.");
            }

            var created = new JsonStoreManifest(_target.ExpectedInstanceId.Value, _storeKind, _schemaVersion, _settings.Encoding.Fingerprint);
            JsonAtomicDocument.Replace(
                _root.ManifestPath,
                JsonStoreSerialization.Encode(created, _settings.Encoding.DocumentOptions, _settings.MaximumDocumentBytes),
                cancellationToken);
            return;
        }

        var manifest = JsonStoreSerialization.Decode<JsonStoreManifest>(payload, _settings.Encoding.DocumentOptions);
        if (manifest.StoreId != _target.ExpectedInstanceId.Value || !string.Equals(manifest.StoreKind, _storeKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The JSON memory-store identity does not match bootstrap configuration.");
        }

        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw new InvalidOperationException("The JSON memory-store schema version is unsupported.");
        }

        if (!string.Equals(manifest.FormatFingerprint, _settings.Encoding.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The JSON memory-store root was written under a different encoding contract.");
        }
    }
}
