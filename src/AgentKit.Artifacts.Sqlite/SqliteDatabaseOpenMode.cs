// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Declares whether a missing SQLite database file may be created.</summary>
public enum SqliteDatabaseOpenMode
{
    /// <summary>The database file must already exist.</summary>
    OpenExisting = 0,

    /// <summary>A missing database file is created.</summary>
    CreateIfMissing = 1,
}
