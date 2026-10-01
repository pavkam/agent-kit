// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Declares how an uninitialized or older schema is treated.</summary>
public enum SqliteSchemaMode
{
    /// <summary>The schema must already match exactly; nothing is created or altered.</summary>
    ValidateExact = 0,

    /// <summary>A missing schema is created by the package's own known migration.</summary>
    ApplyKnownMigrations = 1,
}
