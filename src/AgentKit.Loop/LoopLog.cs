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

    /// <summary>Logs that caller cancellation interrupted a tool batch after at least one call had already started.</summary>
    /// <remarks>Every call in the batch still commits with a matching terminal result before this cancellation propagates.</remarks>
    [LoggerMessage(1033, LogLevel.Information, "Tool batch for turn {TurnId} in run {RunId} was interrupted by cancellation; every call still committed a matching terminal result.")]
    internal static partial void ToolBatchInterrupted(ILogger logger, RunId runId, TurnId turnId);

    /// <summary>Logs a session commit failure without session content.</summary>
    [LoggerMessage(1040, LogLevel.Warning, "Session commit for session {SessionId} failed with outcome {Outcome}.")]
    internal static partial void SessionCommitFailed(ILogger logger, SessionId sessionId, string outcome);

    /// <summary>Logs an append rebasing onto a newer version after a concurrent writer advanced the branch.</summary>
    /// <remarks>A tool that commits its own session entries mid-turn (the plan/todo tool, for one) is the
    /// expected source of this contention; see the remarks on <c>DefaultAgentLoop._maxAppendConflictRetries</c>.</remarks>
    [LoggerMessage(
        1041,
        LogLevel.Information,
        "Session {SessionId} append expected version {ExpectedVersion} but the branch had already advanced to " +
            "{ActualVersion}; retrying at the new version (attempt {Attempt}).")]
    internal static partial void SessionAppendConflictRetried(
        ILogger logger,
        SessionId sessionId,
        SessionVersion expectedVersion,
        SessionVersion actualVersion,
        int attempt);

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

    /// <summary>Logs an isolated run-observer failure without recording event content.</summary>
    [LoggerMessage(1070, LogLevel.Warning, "Run observer for run {RunId} failed while receiving {EventType}; the run continues.")]
    internal static partial void RunObserverFailed(ILogger logger, RunId runId, string eventType);

    /// <summary>Logs a tool invoker fault that was converted into a failed terminal result so the call still settles.</summary>
    [LoggerMessage(1034, LogLevel.Error, "Tool call {ToolCallId} in run {RunId} faulted with error type {ErrorType}; a failed terminal result was recorded.")]
    internal static partial void ToolCallFaulted(ILogger logger, RunId runId, ToolCallId toolCallId, string errorType);

    /// <summary>Logs that the turn limit settled pending tool calls with rejected terminal results instead of invoking them.</summary>
    [LoggerMessage(1035, LogLevel.Information, "Turn limit reached for run {RunId} at turn {TurnId}; {ToolCount} requested tool calls were settled as rejected without invocation.")]
    internal static partial void ToolBatchRejectedAtTurnLimit(ILogger logger, RunId runId, TurnId turnId, int toolCount);

    /// <summary>Logs a model response that could not be accepted as a complete turn because its stop reason or tool-call identities were invalid.</summary>
    [LoggerMessage(1024, LogLevel.Warning, "Model response for run {RunId} turn {TurnId} was not accepted as complete: {Reason}.")]
    internal static partial void ModelResponseNotAccepted(ILogger logger, RunId runId, TurnId turnId, string reason);
}
