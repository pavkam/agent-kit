// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Defines allocation-efficient in-memory session-store events without entry or identity content.</summary>
internal static partial class SessionStoreLog
{
    /// <summary>Records one typed terminal store outcome using causal domain identities.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="operation">The bounded store operation name.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="sessionId">The session identity when already established.</param>
    /// <param name="outcome">The bounded terminal success, rejection, cancellation, or failure classification.</param>
    [LoggerMessage(16000, LogLevel.Debug, "In-memory session operation {Operation} for agent {AgentId} and session {SessionId} completed with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId,
        string outcome);

    /// <summary>Records an unexpected store exception type without exception or session content.</summary>
    /// <param name="logger">The configured Microsoft logger.</param>
    /// <param name="operation">The bounded store operation name.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="sessionId">The session identity when already established.</param>
    /// <param name="errorType">The stable exception type name.</param>
    [LoggerMessage(16001, LogLevel.Error, "In-memory session operation {Operation} for agent {AgentId} and session {SessionId} failed with error type {ErrorType}.")]
    internal static partial void Failed(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId,
        string errorType);
}
