// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Defines content-free structured logs emitted by the SQLite memory-storage lifecycle.</summary>
/// <remarks>Event identifiers 32200-32209 are owned by this package.</remarks>
internal static partial class SqliteMemoryLog
{
    /// <summary>Records a SQLite commit failure by bounded code only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="family">The bounded state family.</param>
    /// <param name="operation">The bounded operation.</param>
    /// <param name="sqliteErrorCode">The SQLite primary error code.</param>
    [LoggerMessage(EventId = 32200, Level = LogLevel.Error, Message = "SQLite {Family} store operation {Operation} failed to commit with code {SqliteErrorCode}.")]
    internal static partial void CommitFailed(ILogger logger, string family, string operation, int sqliteErrorCode);
}
