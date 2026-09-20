// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <content>Contains manifest binding, durable appends, replay, and compaction for the JSON session store.</content>
public sealed partial class JsonSessionStore
{
    /// <summary>Appends one accepted transition and flushes it before the caller mutates the projection.</summary>
    /// <param name="record">The complete record describing the transition about to be applied.</param>
    /// <param name="cancellationToken">Cancels before any byte is written.</param>
    /// <remarks>
    /// Every commit path calls this immediately before its first mutation, so a failed or cancelled write leaves the
    /// projection exactly as it was and the caller observes the failure rather than a phantom success. While replay is
    /// rebuilding the projection from the log the append is suppressed, because the record being applied is already on disk.
    /// </remarks>
    private void Persist(JsonSessionStoreLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A complete transition record is required.");
        Debug.Assert(_replaying || _exclusive is not null, "Live appends happen only while the exclusive lock is held.");
        if (_replaying)
        {
            return;
        }

        _log.Append(EncodeRecord(record), cancellationToken);
    }

    /// <summary>Encodes one transition under the effective record contract and enforces the configured byte bound.</summary>
    /// <param name="record">The complete record to encode.</param>
    /// <returns>The exact compact UTF-8 bytes of one log line, excluding its terminating newline.</returns>
    private byte[] EncodeRecord(JsonSessionStoreLogRecord record)
    {
        Debug.Assert(record is not null, "A complete transition record is required.");
        return JsonStoreSerialization.Encode(record, _recordEncoding.RecordOptions, _settings.MaximumRecordBytes);
    }

    /// <summary>Creates or verifies the manifest that binds this root to one identity, schema, and encoding contract.</summary>
    /// <param name="cancellationToken">Cancels before a newly created manifest is renamed over the target.</param>
    /// <exception cref="InvalidOperationException">The manifest is missing, names another store, or was written under a different schema or encoding.</exception>
    private void BindManifest(CancellationToken cancellationToken)
    {
        Debug.Assert(_exclusive is not null, "The advisory exclusive lock is acquired before manifest binding.");
        var payload = JsonAtomicDocument.Read(_root.ManifestPath, _settings.MaximumDocumentBytes);
        if (payload is null)
        {
            if (_target.OpenMode != JsonStoreOpenMode.CreateIfMissing)
            {
                throw Unavailable("The configured JSON session-store root has no manifest.");
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
            throw Unavailable("The JSON session-store identity does not match bootstrap configuration.");
        }
        if (manifest.SchemaVersion != _schemaVersion)
        {
            throw Unavailable("The JSON session-store schema version is unsupported.");
        }
        if (!string.Equals(manifest.FormatFingerprint, _recordEncoding.Fingerprint, StringComparison.Ordinal))
        {
            throw Unavailable("The JSON session-store root was written under a different encoding contract.");
        }
    }

    /// <summary>Rebuilds the complete projection by re-executing every acknowledged transition in append order.</summary>
    /// <param name="cancellationToken">Cancels between applied records.</param>
    /// <exception cref="InvalidOperationException">The log ends in an unrecoverable torn append or a record cannot be applied.</exception>
    /// <remarks>
    /// Replay drives the same commit paths live callers use, which is what makes recovered optimistic-concurrency versions,
    /// branch sequence spaces, lane ownership, and idempotency receipts identical to the pre-restart state instead of an
    /// approximation reconstructed from a separate state format.
    /// </remarks>
    private void Replay(CancellationToken cancellationToken)
    {
        Debug.Assert(_sessions.Count == 0, "Replay populates an empty projection.");
        Debug.Assert(_exclusive is not null, "Replay runs only while the exclusive lock is held.");
        var replay = _log.Replay(cancellationToken);
        if (replay.HasIncompleteTrailingRecord && _target.RecoveryMode != JsonStoreRecoveryMode.RecoverTornAppends)
        {
            throw Unavailable("The JSON session-store log ends with an incomplete record.");
        }

        var applied = new List<JsonSessionStoreLogRecord>(replay.Records.Count);
        _replaying = true;
        try
        {
            foreach (var encoded in replay.Records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = JsonStoreSerialization.Decode<JsonSessionStoreLogRecord>(
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

    /// <summary>Re-executes one persisted transition and rejects a log that no longer commits.</summary>
    /// <param name="record">The decoded transition.</param>
    /// <param name="cancellationToken">Cancels before the transition is applied.</param>
    /// <exception cref="InvalidOperationException">A member required by the record's kind is absent, or the transition no longer commits.</exception>
    private void Apply(JsonSessionStoreLogRecord record, CancellationToken cancellationToken)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        Debug.Assert(_replaying, "Persisted transitions are applied only during replay.");
        switch (record.Kind)
        {
            case JsonSessionStoreLogRecordKind.SessionCreated:
                Verify<SessionCreated>(
                    CreateCore(
                        Require(record.Create, "creation"),
                        new BranchId(RequireIdentity(record.NewBranchId, "creation")),
                        RequireInstant(record.CommittedAt, "creation"),
                        cancellationToken),
                    "creation");
                return;
            case JsonSessionStoreLogRecordKind.LaneProvisioned:
                Verify<SessionExecutionLaneProvisioned>(
                    ProvisionLaneCore(Require(record.Provision, "lane provisioning"), cancellationToken),
                    "lane provisioning");
                return;
            case JsonSessionStoreLogRecordKind.EntriesAppended:
                Verify<SessionAppended>(
                    AppendCore(
                        Require(record.Append, "append"),
                        RequireInstant(record.CommittedAt, "append"),
                        cancellationToken),
                    "append");
                return;
            case JsonSessionStoreLogRecordKind.BranchCreated:
                Verify<SessionBranched>(
                    CreateBranchCore(
                        Require(record.Branch, "branch"),
                        new BranchId(RequireIdentity(record.NewBranchId, "branch")),
                        RequireInstant(record.CommittedAt, "branch"),
                        cancellationToken),
                    "branch");
                return;
            case JsonSessionStoreLogRecordKind.SessionDeleted:
                Verify<SessionDeleted>(
                    DeleteCore(Require(record.Delete, "deletion"), cancellationToken), "deletion");
                return;
            case JsonSessionStoreLogRecordKind.InputAdmitted:
                Verify<AcceptedInput>(
                    AdmitInputCore(Require(record.Admit, "admission"), cancellationToken), "admission");
                return;
            case JsonSessionStoreLogRecordKind.RunAccepted:
                Verify<SessionRunAccepted>(
                    AcceptRunCore(Require(record.Start, "run acceptance"), cancellationToken), "run acceptance");
                return;
            case JsonSessionStoreLogRecordKind.RunReleased:
                Verify<SessionRunReleased>(
                    ReleaseRunCore(
                        Require(record.Release, "run release"),
                        RequireInstant(record.CommittedAt, "run release"),
                        cancellationToken),
                    "run release");
                return;
            case JsonSessionStoreLogRecordKind.RunAborted:
                Verify<SessionRunAbortRecorded>(
                    AbortRunCore(
                        Require(record.Abort, "run abort"),
                        RequireInstant(record.CommittedAt, "run abort"),
                        cancellationToken),
                    "run abort");
                return;
            case JsonSessionStoreLogRecordKind.InputPromoted:
                Verify<SessionInputPromoted>(
                    PromoteInputCore(Require(record.Promote, "input promotion"), cancellationToken), "input promotion");
                return;
            default:
                throw Unavailable("A persisted session-store record has an unsupported kind.");
        }
    }

    /// <summary>Atomically rewrites the log from the records that still contribute to live state.</summary>
    /// <param name="applied">The ordered decoded records recovered by this replay.</param>
    /// <param name="cancellationToken">Cancels before the rewritten log is renamed over the original.</param>
    /// <remarks>
    /// <para>
    /// A command log cannot collapse a live session's history, because every accepted transition is part of the
    /// authoritative record and of the idempotency evidence a retry must be reconciled against. Compaction therefore
    /// removes exactly two things: an incomplete trailing append, and the body of a session that no longer exists.
    /// </para>
    /// <para>
    /// Creation and deletion records are always retained, because together they carry the retired creation-retry evidence
    /// and the deletion receipt a later retry still has to observe. A session that currently exists keeps its complete
    /// history, which also keeps a create-delete-recreate sequence at one address replaying in its original order.
    /// </para>
    /// </remarks>
    private void Compact(List<JsonSessionStoreLogRecord> applied, CancellationToken cancellationToken)
    {
        Debug.Assert(applied is not null, "The recovered ordered record set is required.");
        Debug.Assert(_exclusive is not null, "Compaction runs only while the exclusive lock is held.");
        var retained = new List<byte[]>(applied.Count);
        foreach (var record in applied)
        {
            if (Contributes(record))
            {
                retained.Add(EncodeRecord(record));
            }
        }

        _log.Compact(retained, cancellationToken);
    }

    /// <summary>Determines whether one recovered record still contributes to the live projection.</summary>
    /// <param name="record">The decoded record under consideration.</param>
    /// <returns><see langword="true"/> when the record must survive compaction.</returns>
    private bool Contributes(JsonSessionStoreLogRecord record)
    {
        Debug.Assert(record is not null, "A decoded record is required.");
        return record.Kind switch
        {
            JsonSessionStoreLogRecordKind.SessionCreated or JsonSessionStoreLogRecordKind.SessionDeleted => true,
            JsonSessionStoreLogRecordKind.LaneProvisioned =>
                _sessions.ContainsKey(Require(record.Provision, "lane provisioning").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.EntriesAppended =>
                _sessions.ContainsKey(Require(record.Append, "append").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.BranchCreated =>
                _sessions.ContainsKey(Require(record.Branch, "branch").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.InputAdmitted =>
                _sessions.ContainsKey(Require(record.Admit, "admission").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.RunAccepted =>
                _sessions.ContainsKey(Require(record.Start, "run acceptance").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.RunReleased =>
                _sessions.ContainsKey(Require(record.Release, "run release").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.RunAborted =>
                _sessions.ContainsKey(Require(record.Abort, "run abort").Context.ToAddress()),
            JsonSessionStoreLogRecordKind.InputPromoted =>
                _sessions.ContainsKey(Require(record.Promote, "input promotion").Context.ToAddress()),
            _ => throw Unavailable("A persisted session-store record has an unsupported kind."),
        };
    }

    /// <summary>Rejects use of a store that trusted bootstrap has not initialized or that has been disposed.</summary>
    /// <exception cref="ObjectDisposedException">The store was disposed.</exception>
    /// <exception cref="InvalidOperationException">The store was used before <see cref="InitializeAsync"/> completed.</exception>
    private void RequireInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "The JSON session store was used before trusted bootstrap initialization.");
        }
    }

    private static T Require<T>(T? value, string context)
        where T : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value ?? throw Unavailable($"A persisted {context} record omits its required request payload.");
    }

    private static Guid RequireIdentity(Guid? value, string context)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value is { } identity && identity != Guid.Empty
            ? identity
            : throw Unavailable($"A persisted {context} record omits its generated branch identity.");
    }

    private static DateTimeOffset RequireInstant(DateTimeOffset? value, string context)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        return value ?? throw Unavailable($"A persisted {context} record omits its commit instant.");
    }

    private static void Verify<TExpected>(object result, string context)
        where TExpected : class
    {
        Debug.Assert(result is not null, "Every commit path returns a typed terminal result.");
        Debug.Assert(!string.IsNullOrWhiteSpace(context), "A bounded replay context is required.");
        if (result is not TExpected)
        {
            throw Unavailable($"A persisted {context} record no longer commits against the recovered session state.");
        }
    }

    private static InvalidOperationException Unavailable(string message)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A bounded failure message is required.");
        return new InvalidOperationException(message);
    }
}
