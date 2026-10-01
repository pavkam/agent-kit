// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Defines content-free structured logs emitted by the SQLite goal-store lifecycle.</summary>
/// <remarks>Event identifiers 31200-31209 are owned by this package.</remarks>
internal static partial class SqliteGoalStoreLog
{
    /// <summary>Records that a write failed to commit because the database stayed locked or refused it.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="errorCode">The numeric SQLite result code, never message content.</param>
    [LoggerMessage(EventId = 31200, Level = LogLevel.Error, Message = "SQLite goal store {Operation} could not commit (SQLite result {ErrorCode}).")]
    internal static partial void CommitFailed(ILogger logger, string operation, int errorCode);
}
