// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Defines whether trusted bootstrap may install the known session schema.</summary>
public enum SqliteSchemaMode
{
    /// <summary>Accepts only the exact already initialized schema.</summary>
    ValidateExact,
    /// <summary>Permits installing or migrating only schemas compiled into this adapter.</summary>
    ApplyKnownMigrations,
}
