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

    /// <summary>Logs the start of a lane-correlated session operation without session content.</summary>
    [LoggerMessage(6005, LogLevel.Debug, "Starting session operation {Operation} for tenant {TenantId}, agent {AgentId}, session {SessionId}, lane {ExecutionLaneId}, operation {OperationId}, run {RunId}, and turn {TurnId}.")]
    internal static partial void CorrelatedOperationStarted(
        ILogger logger,
        string operation,
        TenantId tenantId,
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId? executionLaneId,
        OperationId operationId,
        RunId? runId,
        TurnId? turnId);

    /// <summary>Logs the bounded terminal outcome of a lane-correlated session operation.</summary>
    [LoggerMessage(6006, LogLevel.Debug, "Completed session operation {Operation} for tenant {TenantId}, agent {AgentId}, session {SessionId}, lane {ExecutionLaneId}, operation {OperationId}, run {RunId}, and turn {TurnId} with outcome {Outcome}.")]
    internal static partial void CorrelatedOperationCompleted(
        ILogger logger,
        string operation,
        TenantId tenantId,
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId? executionLaneId,
        OperationId operationId,
        RunId? runId,
        TurnId? turnId,
        string outcome);

    /// <summary>Logs caller cancellation of a lane-correlated session operation.</summary>
    [LoggerMessage(6007, LogLevel.Information, "Session operation {Operation} for tenant {TenantId}, agent {AgentId}, session {SessionId}, lane {ExecutionLaneId}, operation {OperationId}, run {RunId}, and turn {TurnId} was cancelled.")]
    internal static partial void CorrelatedOperationCancelled(
        ILogger logger,
        string operation,
        TenantId tenantId,
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId? executionLaneId,
        OperationId operationId,
        RunId? runId,
        TurnId? turnId);

    /// <summary>Logs an unexpected lane-correlated session error type without exception or session content.</summary>
    [LoggerMessage(6008, LogLevel.Error, "Session operation {Operation} for tenant {TenantId}, agent {AgentId}, session {SessionId}, lane {ExecutionLaneId}, operation {OperationId}, run {RunId}, and turn {TurnId} faulted with error type {ErrorType}.")]
    internal static partial void CorrelatedOperationFaulted(
        ILogger logger,
        string operation,
        TenantId tenantId,
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId? executionLaneId,
        OperationId operationId,
        RunId? runId,
        TurnId? turnId,
        string errorType);
}
