// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Describes the exact table layout one kind of SQLite artifact database must carry, so a database of one kind is never opened as another.</summary>
/// <param name="DisplayName">The noun naming the database kind in refusal messages.</param>
/// <param name="ExpectedTables">The sorted, pipe-separated user table names the schema consists of.</param>
/// <param name="Statements">The DDL statements that create the tables, including the identity metadata table.</param>
internal sealed record SqliteArtifactSchema(string DisplayName, string ExpectedTables, IReadOnlyList<string> Statements)
{
    private const string _metadataTable =
        "CREATE TABLE artifact_metadata (store_id BLOB NOT NULL CHECK(length(store_id) = 16), schema_version INTEGER NOT NULL)";

    /// <summary>Gets the schema of an artifact store database: entries and content-addressed payloads.</summary>
    internal static SqliteArtifactSchema Store { get; } = new(
        "artifact",
        "artifact_entries|artifact_metadata|artifact_payloads",
        [
            _metadataTable,
            "CREATE TABLE artifact_entries (tenant TEXT NOT NULL, preparation_id BLOB NOT NULL CHECK(length(preparation_id) = 16), document TEXT NOT NULL, PRIMARY KEY(tenant, preparation_id))",
            "CREATE TABLE artifact_payloads (tenant TEXT NOT NULL, content_hash TEXT NOT NULL, content BLOB NOT NULL, PRIMARY KEY(tenant, content_hash))",
        ]);

    /// <summary>Gets the schema of a reference-commit intent store database.</summary>
    internal static SqliteArtifactSchema Intents { get; } = new(
        "artifact reference-commit intent",
        "artifact_metadata|artifact_reference_intents",
        [
            _metadataTable,
            "CREATE TABLE artifact_reference_intents (tenant TEXT NOT NULL, preparation_id BLOB NOT NULL CHECK(length(preparation_id) = 16), document TEXT NOT NULL, PRIMARY KEY(tenant, preparation_id))",
        ]);
}
