// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Defines content-free source-generated events for the SQLite approval-store adapter.</summary>
internal static partial class SqliteApprovalStoreLog
{
    /// <summary>Logs successful terminal store observation.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="approvalRequestId">The approval request identity when established.</param>
    /// <param name="approvalResponseId">The approval response identity when established.</param>
    [LoggerMessage(19020, LogLevel.Debug, "SQLite approval store operation {Operation} completed with outcome {Outcome}; request {ApprovalRequestId}, response {ApprovalResponseId}.")]
    internal static partial void OperationCompleted(ILogger logger, string operation, string outcome,
        string? approvalRequestId, string? approvalResponseId);

    /// <summary>Logs a non-successful terminal store observation without persisted or protected content.</summary>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="failureKind">The normalized bounded failure class.</param>
    /// <param name="approvalRequestId">The approval request identity when established.</param>
    /// <param name="approvalResponseId">The approval response identity when established.</param>
    [LoggerMessage(19021, LogLevel.Warning, "SQLite approval store operation {Operation} completed with outcome {Outcome} and failure kind {FailureKind}; request {ApprovalRequestId}, response {ApprovalResponseId}.")]
    internal static partial void OperationFailed(ILogger logger, string operation, string outcome, string failureKind,
        string? approvalRequestId, string? approvalResponseId);
}
