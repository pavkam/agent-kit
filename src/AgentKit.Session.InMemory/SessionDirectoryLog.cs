// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Defines allocation-efficient content-free session-directory events.</summary>
internal static partial class SessionDirectoryLog
{
    /// <summary>Records one typed terminal directory outcome.</summary>
    /// <param name="logger">The configured Microsoft logger.</param><param name="operation">The bounded directory operation.</param>
    /// <param name="agentId">The owning agent.</param><param name="sessionId">The session when established.</param><param name="outcome">The bounded result kind.</param>
    [LoggerMessage(16002, LogLevel.Debug, "In-memory session directory operation {Operation} for agent {AgentId} and session {SessionId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, string operation, AgentId agentId,
        SessionId? sessionId, string outcome);

    /// <summary>Records an unexpected directory exception type without protected request content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param><param name="operation">The bounded directory operation.</param>
    /// <param name="agentId">The owning agent.</param><param name="sessionId">The session when established.</param><param name="errorType">The stable exception type.</param>
    [LoggerMessage(16003, LogLevel.Error, "In-memory session directory operation {Operation} for agent {AgentId} and session {SessionId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string operation, AgentId agentId,
        SessionId? sessionId, string errorType);
}
