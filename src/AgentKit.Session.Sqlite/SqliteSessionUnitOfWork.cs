// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>
/// Executes exactly the narrow, indexed SQL a session-store operation needs against the relational
/// schema declared by <see cref="SqliteSessionSchema"/>, inside one caller-owned connection and
/// transaction.
/// </summary>
/// <remarks>
/// <para>
/// Every method here reads or writes precisely the rows one operation needs: one session row, one
/// branch's tip metadata, a bounded page of entries, one lane row, one admission row, or one
/// idempotency receipt. Nothing loads or decodes another session's data, and ordinary mutations
/// (append, admission, lane provisioning, run acceptance, run release) never decode a previously
/// committed entry payload at all. Only paged reads and branch forks touch entry payloads, and forks
/// copy their raw encoded bytes without decoding them.
/// </para>
/// <para>
/// Instances are confined to the caller's transaction and are not thread-safe; callers create one
/// per <see cref="SqliteSessionDatabase.RunReadAsync{TResult}"/> or
/// <see cref="SqliteSessionDatabase.RunWriteAsync{TResult}"/> invocation.
/// </para>
/// </remarks>
internal sealed partial class SqliteSessionUnitOfWork
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly ISessionEntryCodecCatalog _entryCodecs;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes a unit of work bound to one open connection and transaction.</summary>
    /// <param name="connection">The caller-owned open connection.</param>
    /// <param name="transaction">The caller-owned active transaction.</param>
    /// <param name="entryCodecs">The captured portable entry codec catalog.</param>
    /// <param name="json">The serializer options used for idempotency receipts, admissions, and lane accepted state.</param>
    internal SqliteSessionUnitOfWork(
        SqliteConnection connection, SqliteTransaction transaction, ISessionEntryCodecCatalog entryCodecs, JsonSerializerOptions json)
    {
        Debug.Assert(connection is not null, "The database owns an open connection.");
        Debug.Assert(transaction is not null, "The database owns an active transaction.");
        Debug.Assert(entryCodecs is not null, "The store captures a codec catalog.");
        Debug.Assert(json is not null, "The database owns shared serializer options.");
        _connection = connection;
        _transaction = transaction;
        _entryCodecs = entryCodecs;
        _json = json;
    }

    private SqliteCommand CreateCommand(string sql)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        return command;
    }

    // ---- Sessions ----

    /// <summary>Loads exactly one session's metadata row.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The session row, or <see langword="null"/> when no such session exists.</returns>
    internal async ValueTask<SessionRecord?> GetSessionAsync(SessionAddress address, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT conversation_id, tenant_id, owner_id, active_branch_id, created_at, updated_at, lifecycle_state, version
            FROM {SqliteSessionSchema.SessionsTable} WHERE agent_id = $agent AND session_id = $session;
            """);
        AddAddress(command, address);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return !await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? null
            : new SessionRecord(
                address,
                reader.IsDBNull(0) ? null : new ConversationId(Guid.Parse(reader.GetString(0))),
                new TenantId(reader.GetString(1)),
                new PrincipalId(reader.GetString(2)),
                new BranchId(Guid.Parse(reader.GetString(3))),
                ParseTimestamp(reader.GetString(4)),
                ParseTimestamp(reader.GetString(5)),
                (SessionLifecycleState) reader.GetInt32(6),
                reader.GetInt64(7));
    }

    /// <summary>Inserts a brand-new session row together with its initial empty root branch.</summary>
    /// <param name="record">The complete new session metadata.</param>
    /// <param name="rootBranchId">The initial active branch identity.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask InsertSessionAsync(SessionRecord record, BranchId rootBranchId, CancellationToken cancellationToken)
    {
        await using (var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.SessionsTable}
                (agent_id, session_id, conversation_id, tenant_id, owner_id, active_branch_id, created_at, updated_at, lifecycle_state, version)
            VALUES ($agent, $session, $conversation, $tenant, $owner, $branch, $created, $updated, $state, $version);
            """))
        {
            AddAddress(command, record.Address);
            _ = command.Parameters.AddWithValue("$conversation", record.ConversationId is { } c ? ToText(c.Value) : DBNull.Value);
            _ = command.Parameters.AddWithValue("$tenant", record.TenantId.Value);
            _ = command.Parameters.AddWithValue("$owner", record.OwnerId.Value);
            _ = command.Parameters.AddWithValue("$branch", ToText(record.ActiveBranchId.Value));
            _ = command.Parameters.AddWithValue("$created", ToTimestamp(record.CreatedAt));
            _ = command.Parameters.AddWithValue("$updated", ToTimestamp(record.UpdatedAt));
            _ = command.Parameters.AddWithValue("$state", (int) record.State);
            _ = command.Parameters.AddWithValue("$version", record.Version);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertBranchAsync(record.Address, rootBranchId, parentBranchId: null, forkSequence: 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Advances a session's version and last-updated timestamp after a committed mutation.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="expectedVersion">The version observed at the start of this transaction.</param>
    /// <param name="newVersion">The new version to commit.</param>
    /// <param name="updatedAt">The commit timestamp.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="InvalidOperationException">
    /// No row matched <paramref name="expectedVersion"/>; the immediate transaction should make this unreachable.
    /// </exception>
    internal async ValueTask UpdateSessionVersionAsync(
        SessionAddress address, long expectedVersion, long newVersion, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            UPDATE {SqliteSessionSchema.SessionsTable} SET version = $version, updated_at = $updated
            WHERE agent_id = $agent AND session_id = $session AND version = $expected;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$version", newVersion);
        _ = command.Parameters.AddWithValue("$updated", ToTimestamp(updatedAt));
        _ = command.Parameters.AddWithValue("$expected", expectedVersion);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
        {
            throw new InvalidOperationException(
                "The session version changed between its transactional read and write; the immediate transaction lock should make this unreachable.");
        }
    }

    /// <summary>Deletes a session and every row it owns across every table.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask DeleteSessionCascadeAsync(SessionAddress address, CancellationToken cancellationToken)
    {
        foreach (var table in new[]
        {
            SqliteSessionSchema.EntriesTable, SqliteSessionSchema.AdmissionsTable, SqliteSessionSchema.LanesTable,
            SqliteSessionSchema.BranchesTable, SqliteSessionSchema.SessionScopeIdempotencyTable, SqliteSessionSchema.SessionsTable,
        })
        {
            await using var command = CreateCommand($"DELETE FROM {table} WHERE agent_id = $agent AND session_id = $session;");
            AddAddress(command, address);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // ---- Branches ----

    /// <summary>Loads one branch's cheap tip metadata without decoding any entry.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The branch identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tip metadata, or <see langword="null"/> when the branch does not exist.</returns>
    internal async ValueTask<SqliteBranchTip?> GetBranchTipAsync(SessionAddress address, BranchId branchId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT tip_sequence, tip_entry_id FROM {SqliteSessionSchema.BranchesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return !await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? null
            : new SqliteBranchTip(reader.GetInt64(0), reader.IsDBNull(1) ? null : new SessionEntryId(Guid.Parse(reader.GetString(1))));
    }

    /// <summary>Inserts a new empty branch row.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The new branch identity.</param>
    /// <param name="parentBranchId">The forked-from branch, or <see langword="null"/> for the session's root branch.</param>
    /// <param name="forkSequence">The parent-branch sequence the fork was taken at.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask InsertBranchAsync(
        SessionAddress address, BranchId branchId, BranchId? parentBranchId, long forkSequence, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.BranchesTable}
                (agent_id, session_id, branch_id, parent_branch_id, fork_sequence, tip_sequence, tip_entry_id)
            VALUES ($agent, $session, $branch, $parent, $fork, 0, NULL);
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        _ = command.Parameters.AddWithValue("$parent", parentBranchId is { } parent ? ToText(parent.Value) : DBNull.Value);
        _ = command.Parameters.AddWithValue("$fork", forkSequence);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Determines whether a fork point names the empty prefix or an entry actually committed on the parent branch.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The parent branch.</param>
    /// <param name="atSequence">The requested fork sequence.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when <paramref name="atSequence"/> is zero or names a committed entry on this exact branch.</returns>
    internal async ValueTask<bool> IsCommittedForkPointAsync(
        SessionAddress address, BranchId branchId, long atSequence, CancellationToken cancellationToken)
    {
        if (atSequence == 0)
        {
            return true;
        }

        await using var command = CreateCommand($"""
            SELECT 1 FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch AND sequence = $sequence LIMIT 1;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        _ = command.Parameters.AddWithValue("$sequence", atSequence);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    /// <summary>Copies a parent branch's committed rows up to and including one sequence into a fresh branch, without decoding them.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="sourceBranchId">The parent branch.</param>
    /// <param name="destinationBranchId">The new branch already inserted by <see cref="InsertBranchAsync"/>.</param>
    /// <param name="uptoSequenceInclusive">The inclusive parent-branch fork sequence.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask CopyEntriesForForkAsync(
        SessionAddress address, BranchId sourceBranchId, BranchId destinationBranchId, long uptoSequenceInclusive,
        CancellationToken cancellationToken)
    {
        await using (var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.EntriesTable}
                (agent_id, session_id, branch_id, sequence, entry_id, causal_parent_id, recorded_at, schema_version, codec_type_id, message_id, payload)
            SELECT agent_id, session_id, $destination, sequence, entry_id, causal_parent_id, recorded_at, schema_version, codec_type_id, message_id, payload
            FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $source AND sequence <= $upto;
            """))
        {
            AddAddress(command, address);
            _ = command.Parameters.AddWithValue("$destination", ToText(destinationBranchId.Value));
            _ = command.Parameters.AddWithValue("$source", ToText(sourceBranchId.Value));
            _ = command.Parameters.AddWithValue("$upto", uptoSequenceInclusive);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (uptoSequenceInclusive == 0)
        {
            return;
        }

        await using var tipCommand = CreateCommand($"""
            SELECT entry_id FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch AND sequence = $upto;
            """);
        AddAddress(tipCommand, address);
        _ = tipCommand.Parameters.AddWithValue("$branch", ToText(destinationBranchId.Value));
        _ = tipCommand.Parameters.AddWithValue("$upto", uptoSequenceInclusive);
        var tipEntryId = (string?) await tipCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (tipEntryId is not null)
        {
            await UpdateBranchTipAsync(
                address, destinationBranchId, uptoSequenceInclusive, new SessionEntryId(Guid.Parse(tipEntryId)), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Updates a branch's cached tip metadata after entries were appended to it.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The branch identity.</param>
    /// <param name="tipSequence">The branch's new entry count.</param>
    /// <param name="tipEntryId">The identity of the new last committed entry.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async ValueTask UpdateBranchTipAsync(
        SessionAddress address, BranchId branchId, long tipSequence, SessionEntryId tipEntryId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            UPDATE {SqliteSessionSchema.BranchesTable} SET tip_sequence = $tip, tip_entry_id = $entry
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        _ = command.Parameters.AddWithValue("$tip", tipSequence);
        _ = command.Parameters.AddWithValue("$entry", ToText(tipEntryId.Value));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // ---- Entries ----

    /// <summary>Determines whether an entry identity is already reserved anywhere in this session.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="entryId">The candidate entry identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal async ValueTask<bool> EntryIdReservedAsync(SessionAddress address, SessionEntryId entryId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT 1 FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND entry_id = $entry LIMIT 1;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$entry", ToText(entryId.Value));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>Determines whether a message identity is already reserved anywhere in this session.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="messageId">The candidate message identity.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal async ValueTask<bool> MessageIdReservedAsync(SessionAddress address, MessageId messageId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT 1 FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND message_id = $message LIMIT 1;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$message", ToText(messageId.Value));
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>
    /// Encodes and inserts one already-preflighted entry row, and advances its branch's cached tip metadata.
    /// </summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The branch the entry commits to.</param>
    /// <param name="entry">The entry to persist; the caller has already proven it encodes successfully and within bounds.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="InvalidOperationException">The codec catalog unexpectedly rejects an entry the caller already preflighted.</exception>
    internal async ValueTask InsertEntryAsync(SessionAddress address, BranchId branchId, SessionEntry entry, CancellationToken cancellationToken)
    {
        if (_entryCodecs.Encode(entry) is not SessionEntryEncoded encoded)
        {
            throw new InvalidOperationException(
                "The codec catalog rejected an entry that the caller's preflight already proved encodable.");
        }

        var messageId = entry is MessageSessionEntry message ? message.Message.Id.Value.ToString("D") : null;
        await using (var command = CreateCommand($"""
            INSERT INTO {SqliteSessionSchema.EntriesTable}
                (agent_id, session_id, branch_id, sequence, entry_id, causal_parent_id, recorded_at, schema_version, codec_type_id, message_id, payload)
            VALUES ($agent, $session, $branch, $sequence, $entry, $parent, $recorded, $schema, $codec, $message, $payload);
            """))
        {
            AddAddress(command, address);
            _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
            _ = command.Parameters.AddWithValue("$sequence", entry.Sequence.Value);
            _ = command.Parameters.AddWithValue("$entry", ToText(entry.Id.Value));
            _ = command.Parameters.AddWithValue("$parent", entry.CausalParentId is { } parent ? ToText(parent.Value) : DBNull.Value);
            _ = command.Parameters.AddWithValue("$recorded", ToTimestamp(entry.RecordedAt));
            _ = command.Parameters.AddWithValue("$schema", entry.SchemaVersion.Value);
            _ = command.Parameters.AddWithValue("$codec", encoded.Wire.TypeId.Value);
            _ = command.Parameters.AddWithValue("$message", (object?) messageId ?? DBNull.Value);
            _ = command.Parameters.AddWithValue("$payload", encoded.Wire.Payload.AsSpan().ToArray());
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await UpdateBranchTipAsync(address, branchId, entry.Sequence.Value, entry.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads and decodes one ordered, bounded window of a branch's committed entries.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The branch to read.</param>
    /// <param name="fromSequenceExclusive">The exclusive lower bound.</param>
    /// <param name="toSequenceInclusive">The inclusive upper bound (a snapshot's pinned prefix).</param>
    /// <param name="limit">The maximum number of entries to return.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decoded page, in ascending sequence order.</returns>
    /// <exception cref="JsonException">A codec rejects a persisted payload as malformed or unavailable.</exception>
    internal async ValueTask<ImmutableArray<SessionEntry>> ReadEntriesAsync(
        SessionAddress address, BranchId branchId, long fromSequenceExclusive, long toSequenceInclusive, int limit,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT sequence, codec_type_id, schema_version, payload FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch
                AND sequence > $from AND sequence <= $to
            ORDER BY sequence ASC LIMIT $limit;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        _ = command.Parameters.AddWithValue("$from", fromSequenceExclusive);
        _ = command.Parameters.AddWithValue("$to", toSequenceInclusive);
        _ = command.Parameters.AddWithValue("$limit", limit);
        var builder = ImmutableArray.CreateBuilder<SessionEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var sequence = reader.GetInt64(0);
            var wire = new SessionEntryWireEnvelope(
                new SessionEntryTypeId(reader.GetString(1)), new SchemaVersion(reader.GetString(2)), [.. (byte[]) reader[3]]);
            builder.Add(_entryCodecs.Decode(wire) switch
            {
                SessionEntryDecoded decoded => decoded.Decoded.Entry,
                SessionEntryDecodeRejected rejected => throw new JsonException(
                    $"The persisted entry at sequence {sequence} is malformed: {rejected.Reason}"),
                _ => throw new JsonException($"The persisted entry at sequence {sequence} has no available codec."),
            });
        }

        return builder.ToImmutable();
    }

    /// <summary>Determines whether any entry exists at or beyond a sequence within the given upper bound.</summary>
    /// <param name="address">The addressed session.</param>
    /// <param name="branchId">The branch to probe.</param>
    /// <param name="afterSequence">The exclusive lower bound.</param>
    /// <param name="uptoSequenceInclusive">The inclusive upper bound.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal async ValueTask<bool> HasMoreEntriesAsync(
        SessionAddress address, BranchId branchId, long afterSequence, long uptoSequenceInclusive, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand($"""
            SELECT 1 FROM {SqliteSessionSchema.EntriesTable}
            WHERE agent_id = $agent AND session_id = $session AND branch_id = $branch
                AND sequence > $from AND sequence <= $to LIMIT 1;
            """);
        AddAddress(command, address);
        _ = command.Parameters.AddWithValue("$branch", ToText(branchId.Value));
        _ = command.Parameters.AddWithValue("$from", afterSequence);
        _ = command.Parameters.AddWithValue("$to", uptoSequenceInclusive);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static void AddAddress(SqliteCommand command, SessionAddress address)
    {
        _ = command.Parameters.AddWithValue("$agent", ToText(address.AgentId.Value));
        _ = command.Parameters.AddWithValue("$session", ToText(address.SessionId.Value));
    }

    private static string ToText(Guid value) => value.ToString("D");

    private static string ToTimestamp(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>Serializes one value through the shared receipt/admission/lane serializer options.</summary>
    private byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, _json);

    /// <summary>Deserializes one value through the shared receipt/admission/lane serializer options.</summary>
    private T Deserialize<T>(byte[] payload) =>
        JsonSerializer.Deserialize<T>(payload, _json) ?? throw new JsonException($"The persisted {typeof(T).Name} payload is null.");
}
