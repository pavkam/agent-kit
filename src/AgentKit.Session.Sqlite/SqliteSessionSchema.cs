// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Declares the relational session-store schema: table names and bootstrap DDL.</summary>
/// <remarks>
/// Every session, branch, entry, lane, admission, and idempotency receipt is its own row instead of a field inside
/// one whole-store JSON blob. A decode failure or lock contention on one session's rows never touches another
/// session's rows, and lookups that used to be in-process dictionary scans (admission-by-input, entry-identity
/// reservation, message-identity reservation, pending-admission counts) are real indexed SQL queries.
/// </remarks>
internal static class SqliteSessionSchema
{
    /// <summary>The bootstrap/version marker table name.</summary>
    internal const string SchemaTable = "agentkit_session_schema";
    /// <summary>The one-row-per-session table name.</summary>
    internal const string SessionsTable = "agentkit_sessions";
    /// <summary>The one-row-per-branch table name.</summary>
    internal const string BranchesTable = "agentkit_session_branches";
    /// <summary>The one-row-per-entry table name.</summary>
    internal const string EntriesTable = "agentkit_session_entries";
    /// <summary>The one-row-per-lane table name.</summary>
    internal const string LanesTable = "agentkit_session_lanes";
    /// <summary>The one-row-per-admission table name.</summary>
    internal const string AdmissionsTable = "agentkit_session_admissions";
    /// <summary>The one-row-per-receipt table for session- and branch-scoped idempotency.</summary>
    internal const string SessionScopeIdempotencyTable = "agentkit_session_scope_idempotency";
    /// <summary>The one-row-per-receipt table for tenant/agent-scoped create and delete idempotency.</summary>
    internal const string StoreScopeIdempotencyTable = "agentkit_store_scope_idempotency";

    /// <summary>The bootstrap DDL applied only under <see cref="SqliteSchemaMode.ApplyKnownMigrations"/>.</summary>
    internal const string CreateSchema = $"""
        CREATE TABLE IF NOT EXISTS {SchemaTable} (
            singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
            schema_version INTEGER NOT NULL,
            store_instance_id TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS {SessionsTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            conversation_id TEXT NULL,
            tenant_id TEXT NOT NULL,
            owner_id TEXT NOT NULL,
            active_branch_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            lifecycle_state INTEGER NOT NULL,
            version INTEGER NOT NULL,
            PRIMARY KEY (agent_id, session_id)
        );

        CREATE TABLE IF NOT EXISTS {BranchesTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            branch_id TEXT NOT NULL,
            parent_branch_id TEXT NULL,
            fork_sequence INTEGER NOT NULL DEFAULT 0,
            tip_sequence INTEGER NOT NULL DEFAULT 0,
            tip_entry_id TEXT NULL,
            PRIMARY KEY (agent_id, session_id, branch_id),
            FOREIGN KEY (agent_id, session_id) REFERENCES {SessionsTable} (agent_id, session_id)
        );

        CREATE TABLE IF NOT EXISTS {EntriesTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            branch_id TEXT NOT NULL,
            sequence INTEGER NOT NULL,
            entry_id TEXT NOT NULL,
            causal_parent_id TEXT NULL,
            recorded_at TEXT NOT NULL,
            schema_version TEXT NOT NULL,
            codec_type_id TEXT NOT NULL,
            message_id TEXT NULL,
            payload BLOB NOT NULL,
            PRIMARY KEY (agent_id, session_id, branch_id, sequence),
            FOREIGN KEY (agent_id, session_id, branch_id) REFERENCES {BranchesTable} (agent_id, session_id, branch_id)
        );
        CREATE INDEX IF NOT EXISTS ix_agentkit_session_entries_entry_id
            ON {EntriesTable} (agent_id, session_id, entry_id);
        CREATE INDEX IF NOT EXISTS ix_agentkit_session_entries_message_id
            ON {EntriesTable} (agent_id, session_id, message_id) WHERE message_id IS NOT NULL;

        CREATE TABLE IF NOT EXISTS {LanesTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            lane_id TEXT NOT NULL,
            branch_id TEXT NOT NULL,
            branch_cursor_entry_id TEXT NULL,
            revision INTEGER NOT NULL,
            accepted_state BLOB NULL,
            abort_requested INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (agent_id, session_id, lane_id),
            FOREIGN KEY (agent_id, session_id) REFERENCES {SessionsTable} (agent_id, session_id)
        );

        CREATE TABLE IF NOT EXISTS {AdmissionsTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            admission_id TEXT NOT NULL,
            input_id TEXT NOT NULL,
            execution_lane_id TEXT NOT NULL,
            admitted_sequence INTEGER NOT NULL,
            promoted_sequence INTEGER NULL,
            entry_id TEXT NOT NULL,
            correlation BLOB NOT NULL,
            admitted_input BLOB NOT NULL,
            PRIMARY KEY (agent_id, session_id, admission_id),
            FOREIGN KEY (agent_id, session_id) REFERENCES {SessionsTable} (agent_id, session_id)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_agentkit_session_admissions_input
            ON {AdmissionsTable} (agent_id, session_id, input_id);
        CREATE INDEX IF NOT EXISTS ix_agentkit_session_admissions_pending
            ON {AdmissionsTable} (agent_id, session_id) WHERE promoted_sequence IS NULL;

        CREATE TABLE IF NOT EXISTS {SessionScopeIdempotencyTable} (
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL,
            scope_kind TEXT NOT NULL,
            branch_id TEXT NOT NULL DEFAULT '',
            idempotency_key TEXT NOT NULL,
            request BLOB NOT NULL,
            result BLOB NOT NULL,
            PRIMARY KEY (agent_id, session_id, scope_kind, branch_id, idempotency_key)
        );

        CREATE TABLE IF NOT EXISTS {StoreScopeIdempotencyTable} (
            scope_kind TEXT NOT NULL,
            tenant_id TEXT NOT NULL,
            agent_id TEXT NOT NULL,
            session_id TEXT NOT NULL DEFAULT '',
            idempotency_key TEXT NOT NULL,
            request BLOB NOT NULL,
            result BLOB NULL,
            address_agent_id TEXT NULL,
            address_session_id TEXT NULL,
            PRIMARY KEY (scope_kind, tenant_id, agent_id, session_id, idempotency_key)
        );
        CREATE INDEX IF NOT EXISTS ix_agentkit_store_scope_idempotency_session
            ON {StoreScopeIdempotencyTable} (scope_kind, agent_id, session_id);
        CREATE INDEX IF NOT EXISTS ix_agentkit_store_scope_idempotency_address
            ON {StoreScopeIdempotencyTable} (scope_kind, address_agent_id, address_session_id);
        """;

    /// <summary>The set of tables validated to exist under every schema mode.</summary>
    internal static readonly ImmutableArray<string> RequiredTables =
    [
        SchemaTable, SessionsTable, BranchesTable, EntriesTable, LanesTable, AdmissionsTable,
        SessionScopeIdempotencyTable, StoreScopeIdempotencyTable,
    ];
}
