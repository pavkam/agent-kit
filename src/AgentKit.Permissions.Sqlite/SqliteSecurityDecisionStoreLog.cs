// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Defines content-free source-generated events for the SQLite security-decision-store adapter.</summary>
internal static partial class SqliteSecurityDecisionStoreLog
{
    /// <summary>Logs successful terminal store observation.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="securityRequestId">The request identity when established.</param>
    [LoggerMessage(19010, LogLevel.Debug, "SQLite security decision store operation {Operation} completed with outcome {Outcome}; request {SecurityRequestId}.")]
    internal static partial void OperationCompleted(ILogger logger, string operation, string outcome,
        string? securityRequestId);

    /// <summary>Logs a non-successful terminal store observation without provider or protected content.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="failureKind">The normalized bounded failure class.</param>
    /// <param name="securityRequestId">The request identity when established.</param>
    [LoggerMessage(19011, LogLevel.Warning, "SQLite security decision store operation {Operation} completed with outcome {Outcome} and failure kind {FailureKind}; request {SecurityRequestId}.")]
    internal static partial void OperationFailed(ILogger logger, string operation, string outcome, string failureKind,
        string? securityRequestId);
}
