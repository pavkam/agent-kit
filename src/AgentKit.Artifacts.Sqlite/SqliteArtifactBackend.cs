// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Persists artifact entries and tenant-qualified, content-addressed payloads in one exclusive SQLite database.</summary>
/// <remarks>
/// A plan is committed in a fixed order: the payload is inserted, then every entry of the plan is upserted in one transaction, then
/// released payloads are deleted. A crash between steps can leave unreferenced payload bytes, which recovery sweeps away, but can
/// never leave a committed entry without bytes or readable bytes without an entry. Payloads are keyed by tenant and content hash, so
/// bytes are shared only inside one tenant and only while some live entry references them.
/// </remarks>
internal sealed class SqliteArtifactBackend: ArtifactStateBackend
{
    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly SqliteArtifactDatabase _database;

    internal SqliteArtifactBackend(SqliteArtifactDatabase database, ILogger? logger)
        : base("sqlite", logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <inheritdoc/>
    protected override ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken)
    {
        _database.Open();
        var connection = _database.Connection;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT document FROM artifact_entries ORDER BY rowid";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                State.Restore(JsonStoreSerialization.Decode<StoredArtifactEntry>(System.Text.Encoding.UTF8.GetBytes(reader.GetString(0)), _json).ToDomain());
            }
        }

        SweepUnreferencedPayloads(connection);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO artifact_payloads(tenant, content_hash, content) VALUES ($tenant, $hash, $content)";
        _ = command.Parameters.AddWithValue("$tenant", entry.TenantId.Value);
        _ = command.Parameters.AddWithValue("$hash", entry.ContentHash.Value);
        _ = command.Parameters.AddWithValue("$content", content.ToArray());
        _ = command.ExecuteNonQuery();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken)
    {
        var connection = _database.Connection;
        using var transaction = connection.BeginTransaction(deferred: false);
        foreach (var entry in upserts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = System.Text.Encoding.UTF8.GetString(
                JsonStoreSerialization.Encode(StoredArtifactEntry.FromDomain(entry), _json, _database.Settings.MaximumRecordBytes));
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO artifact_entries(tenant, preparation_id, document) VALUES ($tenant, $preparation, $document) "
                + "ON CONFLICT(tenant, preparation_id) DO UPDATE SET document = excluded.document";
            _ = command.Parameters.AddWithValue("$tenant", entry.TenantId.Value);
            _ = command.Parameters.AddWithValue("$preparation", entry.PreparationId.Value.ToByteArray());
            _ = command.Parameters.AddWithValue("$document", document);
            _ = command.ExecuteNonQuery();
        }

        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken)
    {
        if (!stillReferenced)
        {
            using var command = _database.Connection.CreateCommand();
            command.CommandText = "DELETE FROM artifact_payloads WHERE tenant = $tenant AND content_hash = $hash";
            _ = command.Parameters.AddWithValue("$tenant", entry.TenantId.Value);
            _ = command.Parameters.AddWithValue("$hash", entry.ContentHash.Value);
            _ = command.ExecuteNonQuery();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken)
    {
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT content FROM artifact_payloads WHERE tenant = $tenant AND content_hash = $hash";
        _ = command.Parameters.AddWithValue("$tenant", entry.TenantId.Value);
        _ = command.Parameters.AddWithValue("$hash", entry.ContentHash.Value);
        return ValueTask.FromResult(command.ExecuteScalar() as byte[]);
    }

    /// <inheritdoc/>
    protected override bool IsStorageFailure(Exception exception) =>
        base.IsStorageFailure(exception) || exception is SqliteException;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _database.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SweepUnreferencedPayloads(SqliteConnection connection)
    {
        var orphans = new List<(string Tenant, string Hash)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT tenant, content_hash FROM artifact_payloads";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var tenant = new TenantId(reader.GetString(0));
                var hash = new ContentHash(reader.GetString(1));
                if (!State.HasLivePayload(tenant, hash))
                {
                    orphans.Add((tenant.Value, hash.Value));
                }
            }
        }

        foreach (var (tenant, hash) in orphans)
        {
            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM artifact_payloads WHERE tenant = $tenant AND content_hash = $hash";
            _ = delete.Parameters.AddWithValue("$tenant", tenant);
            _ = delete.Parameters.AddWithValue("$hash", hash);
            _ = delete.ExecuteNonQuery();
        }
    }
}
