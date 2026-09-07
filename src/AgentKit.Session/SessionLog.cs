// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Defines allocation-efficient structured session-coordination log events.</summary>
internal static partial class SessionLog
{
    /// <summary>Logs the start of one session operation without session content.</summary>
    [LoggerMessage(6000, LogLevel.Debug, "Starting session operation {Operation} for agent {AgentId} and session {SessionId}.")]
    internal static partial void OperationStarted(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId);

    /// <summary>Logs the typed terminal outcome of one session operation.</summary>
    [LoggerMessage(6001, LogLevel.Debug, "Completed session operation {Operation} for agent {AgentId} and session {SessionId} with outcome {Outcome}.")]
    internal static partial void OperationCompleted(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId,
        string outcome);

    /// <summary>Logs an unexpected session coordinator or store error type without exception content.</summary>
    [LoggerMessage(6002, LogLevel.Error, "Session operation {Operation} for agent {AgentId} and session {SessionId} faulted with error type {ErrorType}.")]
    internal static partial void OperationFaulted(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId,
        string errorType);

    /// <summary>Logs caller cancellation of one session operation.</summary>
    [LoggerMessage(6004, LogLevel.Information, "Session operation {Operation} for agent {AgentId} and session {SessionId} was cancelled.")]
    internal static partial void OperationCancelled(
        ILogger logger,
        string operation,
        AgentId agentId,
        SessionId? sessionId);

    /// <summary>Logs an isolated best-effort session event sink error type without exception content.</summary>
    [LoggerMessage(6003, LogLevel.Warning, "Session event sink {SinkName} failed for session {SessionId} with error type {ErrorType}; the committed result is unchanged.")]
    internal static partial void EventSinkFailed(
        ILogger logger,
        string sinkName,
        SessionId sessionId,
        string errorType);
}
