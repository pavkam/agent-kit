// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Defines the schema effects granted to trusted SQLite store initialization.</summary>
public enum SqliteSchemaMode
{
    /// <summary>Requires the exact supported schema and WAL mode without applying schema or journal-mode changes.</summary>
    ValidateExact,
    /// <summary>Permits creating the compiled schema and applying only explicitly supported migrations; version one currently has no older accepted version.</summary>
    ApplyKnownMigrations,
}
