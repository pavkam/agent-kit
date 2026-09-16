// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Declares the relational session-directory schema: table names and bootstrap DDL.</summary>
/// <remarks>
/// One row of truth per routed address (owner folded into the same row instead of a second parallel
/// dictionary), one row per creation-retry route, and one row per write-retry route — instead of one
/// whole-directory JSON blob rewritten on every lookup and mutation.
/// </remarks>
internal static class SqliteSessionDirectorySchema
{
    /// <summary>The bootstrap/version marker table name.</summary>
    internal const string SchemaTable = "agentkit_session_directory_schema";
    /// <summary>The one-row-per-address authoritative location table name.</summary>
    internal const string LocationsTable = "agentkit_session_directory_locations";
    /// <summary>The one-row-per-retry creation-route table name.</summary>
    internal const string CreationRoutesTable = "agentkit_session_directory_creation_routes";
    /// <summary>The one-row-per-retry write-route table name.</summary>
    internal const string WriteRoutesTable = "agentkit_session_directory_write_routes";

    /// <summary>The bootstrap DDL applied only under <see cref="SqliteSchemaMode.ApplyKnownMigrations"/>.</summary>
    internal const string CreateSchema = $"""
        CREATE TABLE IF NOT EXISTS {SchemaTable} (
            singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
            schema_version INTEGER NOT NULL,
            store_instance_id TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS {LocationsTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            tenant_id TEXT NOT NULL,
            store_key TEXT NOT NULL,
            directory_revision INTEGER NOT NULL,
            recorded_at TEXT NOT NULL,
            schema_version TEXT NOT NULL,
            owner_principal_id TEXT NOT NULL,
            PRIMARY KEY (agent_id, session_id)
        );
        CREATE INDEX IF NOT EXISTS ix_agentkit_session_directory_locations_owner
            ON {LocationsTable} (tenant_id, agent_id, owner_principal_id);

        CREATE TABLE IF NOT EXISTS {CreationRoutesTable} (
            tenant_id TEXT NOT NULL,
            agent_id TEXT NOT NULL,
            idempotency_key TEXT NOT NULL,
            request BLOB NOT NULL,
            location BLOB NOT NULL,
            PRIMARY KEY (tenant_id, agent_id, idempotency_key)
        );

        CREATE TABLE IF NOT EXISTS {WriteRoutesTable} (
            tenant_id TEXT NOT NULL,
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            idempotency_key TEXT NOT NULL,
            request BLOB NOT NULL,
            location BLOB NOT NULL,
            PRIMARY KEY (tenant_id, agent_id, session_id, idempotency_key)
        );
        """;

    /// <summary>The set of tables validated to exist under every schema mode.</summary>
    internal static readonly ImmutableArray<string> RequiredTables =
        [SchemaTable, LocationsTable, CreationRoutesTable, WriteRoutesTable];
}
