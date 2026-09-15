// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Defines whether trusted bootstrap may create the selected database file.</summary>
public enum SqliteDatabaseOpenMode
{
    /// <summary>Requires an existing main database file.</summary>
    OpenExisting,
    /// <summary>Permits creating the main file inside an existing parent directory.</summary>
    CreateIfMissing,
}
