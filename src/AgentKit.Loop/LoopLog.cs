// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Defines allocation-efficient structured agent-loop log events.</summary>
internal static partial class LoopLog
{
    /// <summary>Logs the start of one correlated agent run.</summary>
    [LoggerMessage(1000, LogLevel.Information, "Starting run {RunId} for agent {AgentId} in session {SessionId}.")]
    internal static partial void RunStarted(ILogger logger, RunId runId, AgentId agentId, SessionId sessionId);

    /// <summary>Logs successful settlement without copying assistant content.</summary>
    [LoggerMessage(1001, LogLevel.Information, "Completed run {RunId} with {MessageCount} newly committed messages.")]
    internal static partial void RunCompleted(ILogger logger, RunId runId, int messageCount);

    /// <summary>Logs a typed terminal run outcome that was not successful.</summary>
    [LoggerMessage(1002, LogLevel.Warning, "Run {RunId} settled without success with outcome {Outcome}.")]
    internal static partial void RunEndedWithoutSuccess(ILogger logger, RunId runId, string outcome);

    /// <summary>Logs cancellation requested by the caller.</summary>
    [LoggerMessage(1003, LogLevel.Information, "Run {RunId} was cancelled by its caller.")]
    internal static partial void RunCancelled(ILogger logger, RunId runId);

    /// <summary>Logs an unexpected loop exception type with structural correlation.</summary>
    [LoggerMessage(1004, LogLevel.Error, "Run {RunId} faulted with error type {ErrorType}.")]
    internal static partial void RunFaulted(ILogger logger, RunId runId, string errorType);

    /// <summary>Logs the start of one numbered turn.</summary>
    [LoggerMessage(1010, LogLevel.Debug, "Starting turn {TurnNumber} ({TurnId}) for run {RunId}.")]
    internal static partial void TurnStarted(ILogger logger, RunId runId, TurnId turnId, int turnNumber);

    /// <summary>Logs the terminal or continuation result of one turn.</summary>
    [LoggerMessage(1011, LogLevel.Debug, "Completed turn {TurnId} for run {RunId} with outcome {Outcome}.")]
    internal static partial void TurnCompleted(ILogger logger, RunId runId, TurnId turnId, string outcome);

    /// <summary>Logs a turn that terminated without successful completion.</summary>
    [LoggerMessage(1012, LogLevel.Warning, "Turn {TurnId} for run {RunId} failed with outcome {Outcome}.")]
    internal static partial void TurnFailed(ILogger logger, RunId runId, TurnId turnId, string outcome);

    /// <summary>Logs dispatch of a model request without prompt or output content.</summary>
    [LoggerMessage(1020, LogLevel.Debug, "Dispatching model request {ModelRequestId} for turn {TurnId} in run {RunId} using alias {ModelAlias}.")]
    internal static partial void ModelRequestStarted(
        ILogger logger,
        RunId runId,
        TurnId turnId,
        ModelRequestId modelRequestId,
        ModelAlias modelAlias);

    /// <summary>Logs the start of a bounded tool batch.</summary>
    [LoggerMessage(1030, LogLevel.Debug, "Starting {ToolCount} tool calls for turn {TurnId} in run {RunId}.")]
    internal static partial void ToolBatchStarted(ILogger logger, RunId runId, TurnId turnId, int toolCount);

    /// <summary>Logs successful completion of a bounded tool batch.</summary>
    [LoggerMessage(1031, LogLevel.Debug, "Completed {ToolCount} tool calls for turn {TurnId} in run {RunId}.")]
    internal static partial void ToolBatchCompleted(ILogger logger, RunId runId, TurnId turnId, int toolCount);

    /// <summary>Logs a tool batch that could not commit its terminal projection.</summary>
    [LoggerMessage(1032, LogLevel.Warning, "Tool batch for turn {TurnId} in run {RunId} failed with outcome {Outcome}.")]
    internal static partial void ToolBatchFailed(ILogger logger, RunId runId, TurnId turnId, string outcome);

    /// <summary>Logs a session commit failure without session content.</summary>
    [LoggerMessage(1040, LogLevel.Warning, "Session commit for session {SessionId} failed with outcome {Outcome}.")]
    internal static partial void SessionCommitFailed(ILogger logger, SessionId sessionId, string outcome);

    /// <summary>Logs a run that ended because no usable model could be chosen.</summary>
    [LoggerMessage(1050, LogLevel.Warning, "Model selection for run {RunId} failed: {Reason}.")]
    internal static partial void ModelSelectionFailed(ILogger logger, RunId runId, string reason);

    /// <summary>Logs one terminal continuation proposal without captured content.</summary>
    [LoggerMessage(1060, LogLevel.Debug, "Continuation evaluation for run {RunId} at {Boundary} proposed {Decision}.")]
    internal static partial void ContinuationEvaluated(ILogger logger, RunId runId, string boundary, string decision);

    /// <summary>Logs cancellation of an uncommitted continuation evaluation.</summary>
    [LoggerMessage(1061, LogLevel.Debug, "Continuation evaluation for run {RunId} was cancelled.")]
    internal static partial void ContinuationCancelled(ILogger logger, RunId runId);

    /// <summary>Logs an unexpected continuation-policy failure by exception type only.</summary>
    [LoggerMessage(1062, LogLevel.Error, "Continuation evaluation for run {RunId} failed with error type {ErrorType}.")]
    internal static partial void ContinuationFailed(ILogger logger, RunId runId, string errorType);
}
