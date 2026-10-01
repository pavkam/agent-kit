// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Persists reference-commit intents as one upserted JSON document per tenant preparation in an exclusive SQLite database.</summary>
/// <remarks>Each change is one atomic statement, so an acknowledged intent or transition is committed and a failed write leaves the previous document intact. Documents are replayed into the shared projection once when the database is first used.</remarks>
internal sealed class SqliteArtifactReferenceCommitIntentBackend: ArtifactReferenceCommitIntentStateBackend
{
    private static readonly JsonSerializerOptions _json = JsonStoreSerialization.CreateCanonicalOptions();

    private readonly SqliteArtifactDatabase _database;

    internal SqliteArtifactReferenceCommitIntentBackend(SqliteArtifactDatabase database, TimeProvider time, ILogger? logger)
        : base("sqlite", time, logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <inheritdoc/>
    protected override void Recover(ArtifactReferenceCommitIntentTable table, CancellationToken cancellationToken)
    {
        _database.Open();
        using var command = _database.Connection.CreateCommand();
        command.CommandText = "SELECT document FROM artifact_reference_intents ORDER BY rowid";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            table.Restore(JsonStoreSerialization.Decode<StoredArtifactReferenceCommitIntent>(System.Text.Encoding.UTF8.GetBytes(reader.GetString(0)), _json).ToDomain());
        }
    }

    /// <inheritdoc/>
    protected override void Persist(ArtifactReferenceCommitIntent intent, CancellationToken cancellationToken)
    {
        var document = System.Text.Encoding.UTF8.GetString(
            JsonStoreSerialization.Encode(StoredArtifactReferenceCommitIntent.FromDomain(intent), _json, _database.Settings.MaximumRecordBytes));
        using var command = _database.Connection.CreateCommand();
        command.CommandText =
            "INSERT INTO artifact_reference_intents(tenant, preparation_id, document) VALUES ($tenant, $preparation, $document) "
            + "ON CONFLICT(tenant, preparation_id) DO UPDATE SET document = excluded.document";
        _ = command.Parameters.AddWithValue("$tenant", intent.TenantId.Value);
        _ = command.Parameters.AddWithValue("$preparation", intent.PreparationId.Value.ToByteArray());
        _ = command.Parameters.AddWithValue("$document", document);
        _ = command.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _database.Dispose();
        }

        base.Dispose(disposing);
    }
}
