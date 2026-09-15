// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Captures complete durable session-directory routing state.</summary>
internal sealed class SqliteSessionDirectoryState
{
    /// <summary>Gets or sets authoritative locations.</summary>
    public List<SessionLocation> Locations { get; set; } = [];
    /// <summary>Gets or sets address owners.</summary>
    public List<SqliteSessionDirectoryOwner> Owners { get; set; } = [];
    /// <summary>Gets or sets exact creation retry routes.</summary>
    public List<CreationRoute> CreationRoutes { get; set; } = [];
    /// <summary>Gets or sets exact directory-write retry routes.</summary>
    public List<DirectoryWriteRoute> WriteRoutes { get; set; } = [];
}
