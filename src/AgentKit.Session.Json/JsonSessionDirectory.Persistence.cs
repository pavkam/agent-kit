// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <content>Contains manifest binding, durable appends, replay, and compaction for the JSON session directory.</content>
public sealed partial class JsonSessionDirectory
{
    /// <summary>Appends one committed route and flushes it before the caller mutates the projection.</summary>
    /// <param name="record">The complete record describing the route about to be applied.</param>
    /// <param name="cancellationToken">Cancels before any byte is written.</param>
    /// <remarks>
    /// Every commit path calls this immediately before its first mutation, so a failed or cancelled write leaves routing
    /// state exactly as it was. While replay is rebuilding the projection the append is suppressed, because the record
    /// being applied is already on disk.
    /// </remarks>
    private void Persist(JsonSessionDirectoryLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A complete routing record is required.");
        Debug.Assert(_replaying || _exclusive is not null, "Live appends happen only while the exclusive lock is held.");
        if (_replaying)
        {
            return;
        }

        _log.Append(EncodeRecord(record), cancellationToken);
    }

    /// <summary>Encodes one routing record under the effective contract and enforces the configured byte bound.</summary>
    /// <param name="record">The complete record to encode.</param>
    /// <returns>The exact compact UTF-8 bytes of one log line, excluding its terminating newline.</returns>
    private byte[] EncodeRecord(JsonSessionDirectoryLogRecord record)
    {
        Debug.Assert(record is not null, "A complete routing record is required.");
        return JsonStoreSerialization.Encode(record, _recordEncoding.RecordOptions, _settings.MaximumRecordBytes);
    }

    /// <summary>Creates or verifies the manifest that binds this root to one identity, schema, and encoding contract.</summary>
    /// <param name="cancellationToken">Cancels before a newly created manifest is renamed over the target.</param>
    /// <exception cref="InvalidOperationException">The manifest is missing, names another directory, or was written under a different schema or encoding.</exception>
    private void BindManifest(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "The advisory exclusive lock is acquired before manifest binding.");
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw Unavailable("The configured JSON session-directory root has no manifest.");
            }

            var created = new JsonStoreManifest(
                _target.ExpectedStoreInstanceId.Value, _storeKind, _schemaVersion, _recordEncoding.Fingerprint);
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
            throw Unavailable("The JSON session-directory identity does not match bootstrap configuration.");
        }
        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable("The JSON session-directory schema version is unsupported.");
        }
        if (!string.Equals(manifest.FormatFingerprint, _recordEncoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable("The JSON session-directory root was written under a different encoding contract.");
        }
    }

    /// <summary>Rebuilds the complete routing projection by re-executing every acknowledged commit in append order.</summary>
    /// <param name="cancellationToken">Cancels between applied records.</param>
    /// <exception cref="InvalidOperationException">The log ends in an unrecoverable torn append or a record no longer commits.</exception>
    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_locations.Count == 0, "Replay populates an empty projection.");
        Debug.Assert(_exclusive is not null, "Replay runs only while the exclusive lock is held.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable("The JSON session-directory log ends with an incomplete record.");
        }

        var applied = new List<JsonSessionDirectoryLogRecord>(replay.Records.Count);
        _replaying = true;
        try
        {
            foreach (var encoded in replay.Records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = JsonStoreSerialization.Decode<JsonSessionDirectoryLogRecord>(
                    encoded.Span, _recordEncoding.RecordOptions);
                Apply(record, cancellationToken);
                applied.Add(record);
            }
        }
        finally
        {
            _replaying = false;
        }

        if (replay.HasIncompleteTrailingRecord || applied.Count > _settings.CompactionRecordThreshold)
        {
            Compact(applied, cancellationToken);
        }
    }

    /// <summary>Re-executes one persisted route and rejects a log that no longer commits.</summary>
    /// <param name="record">The decoded routing record.</param>
    /// <param name="cancellationToken">Cancels before the route is applied.</param>
    /// <exception cref="InvalidOperationException">The request required by the record's kind is absent, or the route no longer commits.</exception>
    private void Apply(JsonSessionDirectoryLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        Debug.Assert(_replaying, "Persisted routes are applied only during replay.");
        var result = record.Kind switch
        {
            JsonSessionDirectoryLogRecordKind.RouteRecorded =>
                RecordCore(Require(record.Write, "route"), cancellationToken),
            JsonSessionDirectoryLogRecordKind.CreationRouteRecorded =>
                RecordCreateCore(Require(record.Create, "creation route"), cancellationToken),
            _ => throw Unavailable("A persisted session-directory record has an unsupported kind."),
        };
        if (result is not SessionLocationRecorded)
        {
            throw Unavailable("A persisted routing record no longer commits against the recovered directory state.");
        }
    }

    /// <summary>Atomically rewrites the log from the recovered ordered record set.</summary>
    /// <param name="applied">The ordered decoded records recovered by this replay.</param>
    /// <param name="cancellationToken">Cancels before the rewritten log is renamed over the original.</param>
    /// <remarks>
    /// A pinned route is never retired and a creation retry identity must remain reconcilable forever, so every recovered
    /// record still contributes to live state. Compaction therefore exists to discard an incomplete trailing append and to
    /// normalize the log under the current contract; it does not drop committed routing history.
    /// </remarks>
    private void Compact(List<JsonSessionDirectoryLogRecord> applied, CancellationToken cancellationToken)
    {
        Debug.Assert(applied is not null, "The recovered ordered record set is required.");
        Debug.Assert(_exclusive is not null, "Compaction runs only while the exclusive lock is held.");
        var retained = new List<byte[]>(applied.Count);
        foreach (var record in applied)
        {
            retained.Add(EncodeRecord(record));
        }

        _log.Compact(retained, cancellationToken);
    }

    /// <summary>Rejects use of a directory that trusted bootstrap has not initialized or that has been disposed.</summary>
    /// <exception cref="ObjectDisposedException">The directory was disposed.</exception>
    /// <exception cref="InvalidOperationException">The directory was used before <see cref="InitializeAsync"/> completed.</exception>
    private void RequireInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "The JSON session directory was used before trusted bootstrap initialization.");
        }
    }

    private static T Require<T>(T? value, string context)
        where T : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value ?? throw Unavailable($"A persisted {context} record omits its required request payload.");
    }

    private static InvalidOperationException Unavailable(string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        return new InvalidOperationException(message);
    }
}
