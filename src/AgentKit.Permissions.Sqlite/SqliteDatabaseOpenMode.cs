// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Defines whether trusted bootstrap may create the exact configured SQLite database file.</summary>
public enum SqliteDatabaseOpenMode
{
    /// <summary>Requires the configured main database file to exist.</summary>
    OpenExisting,
    /// <summary>Permits creation of only the configured main database in its existing parent directory.</summary>
    CreateIfMissing,
}
