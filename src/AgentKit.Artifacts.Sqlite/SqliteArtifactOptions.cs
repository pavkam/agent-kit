// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Configures the lock and size bounds of a SQLite artifact store.</summary>
public sealed class SqliteArtifactOptions
{
    /// <summary>Gets or sets the whole-second wait for another process to release the database.</summary>
    /// <value>At least one whole second. The default is five seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the largest encoded entry document, in bytes.</summary>
    /// <value>A positive bound. The default is one mebibyte; artifact bytes are stored separately and bounded by the coordinator.</value>
    public int MaximumRecordBytes { get; set; } = 1_048_576;
}
