// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Configures the lock and size bounds of a SQLite memory, document, or vector store.</summary>
public sealed class SqliteMemoryOptions
{
    /// <summary>Gets or sets the whole-second wait for another writer to release the database write lock.</summary>
    /// <value>At least one whole second. The default is five seconds.</value>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the largest encoded row document, in bytes.</summary>
    /// <value>A positive bound. The default is sixteen mebibytes, which bounds one document's complete chunk set.</value>
    public int MaximumRecordBytes { get; set; } = 16_777_216;
}
