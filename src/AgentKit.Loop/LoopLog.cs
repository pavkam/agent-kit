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
    /// <remarks>Every call in the batch still commits with a matching terminal result, after which the run settles with a typed <see cref="AgentRunCancelled"/> outcome.</remarks>
    [LoggerMessage(1033, LogLevel.Information, "Tool batch for turn {TurnId} in run {RunId} was interrupted by cancellation; every call still committed a matching terminal result.")]
    internal static partial void ToolBatchInterrupted(ILogger logger, RunId runId, TurnId turnId);

    /// <summary>Logs a session commit failure without session content.</summary>
    [LoggerMessage(1040, LogLevel.Warning, "Session commit for session {SessionId} failed with outcome {Outcome}.")]
    internal static partial void SessionCommitFailed(ILogger logger, SessionId sessionId, string outcome);

    /// <summary>Logs an append rebasing onto a newer version after a concurrent writer advanced the branch.</summary>
    /// <remarks>A tool that commits its own session entries mid-turn (the plan/todo tool, for one) is the
    /// expected source of this contention; the bound is <see cref="AgentLoopOptions.AppendConflictRetryLimit"/>.</remarks>
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

    /// <summary>Logs an assistant append refused because a concurrent writer committed a message the pending response never saw.</summary>
    /// <remarks>The response was generated against stale history, so committing it would misattribute it as a reply to the interleaved message. The run fails closed instead of rebasing.</remarks>
    [LoggerMessage(
        1042,
        LogLevel.Warning,
        "Session {SessionId} append expected version {ExpectedVersion} but a concurrent message advanced the branch to " +
            "{ActualVersion}; the pending model response is stale and was not committed.")]
    internal static partial void SessionAppendStaleAfterInterleavedMessage(
        ILogger logger,
        SessionId sessionId,
        SessionVersion expectedVersion,
        SessionVersion actualVersion);

    /// <summary>Logs a run or turn that settled because the security authority could not capture fresh authorization.</summary>
    /// <remarks>No protected work was attempted under the operation; the safe reason travels in the typed outcome, not the log.</remarks>
    [LoggerMessage(1080, LogLevel.Warning, "Authorization could not be captured for run {RunId} (turn {TurnId}); the run settles without protected work.")]
    internal static partial void AuthorizationCaptureUnavailable(ILogger logger, RunId runId, TurnId? turnId);

    /// <summary>Logs captured authorization that contradicts the run-start evidence, which fails the run closed as invalid state.</summary>
    [LoggerMessage(1081, LogLevel.Error, "Authorization captured for run {RunId} (turn {TurnId}) differs from the run-start evidence; the run settles as invalid state.")]
    internal static partial void AuthorizationEvidenceMismatch(ILogger logger, RunId runId, TurnId? turnId);

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

    /// <summary>Logs the loop applying one continuation proposal to its own transition, without proposal content.</summary>
    [LoggerMessage(1063, LogLevel.Debug, "Run {RunId} turn {TurnId} applied continuation decision {Decision}.")]
    internal static partial void ContinuationDecisionApplied(ILogger logger, RunId runId, TurnId turnId, string decision);

    /// <summary>Logs a tool batch continuing under the canonical committed-tool-results rule because its single-entry projection cannot be described to the continuation policy.</summary>
    /// <remarks>The reduced loop commits one tool message per batch; the continuation contract requires a distinct terminal-record identity per call, which only a single-call batch can supply.</remarks>
    [LoggerMessage(1064, LogLevel.Information, "Run {RunId} turn {TurnId} committed {ToolCount} tool results in one projection entry; the continuation policy was not consulted and the turn continues for interpretation.")]
    internal static partial void ContinuationPolicyBypassedForBatchProjection(ILogger logger, RunId runId, TurnId turnId, int toolCount);

    /// <summary>Logs a required terminal commit that did not complete within the configured settlement bound.</summary>
    /// <remarks>Whether the commit landed is unknown; the run settles as a session operation failure saying so rather than hanging.</remarks>
    [LoggerMessage(1043, LogLevel.Error, "Settlement commit for run {RunId} in session {SessionId} did not complete within {SettlementTimeout}; the commit outcome is unknown.")]
    internal static partial void SettlementTimedOut(ILogger logger, RunId runId, SessionId sessionId, TimeSpan settlementTimeout);

    /// <summary>Logs an isolated run-observer failure without recording event content.</summary>
    [LoggerMessage(1070, LogLevel.Warning, "Run observer for run {RunId} failed while receiving {EventType}; the run continues.")]
    internal static partial void RunObserverFailed(ILogger logger, RunId runId, string eventType);

    /// <summary>Logs a tool invoker fault that was converted into a failed terminal result so the call still settles.</summary>
    [LoggerMessage(1034, LogLevel.Error, "Tool call {ToolCallId} in run {RunId} faulted with error type {ErrorType}; a failed terminal result was recorded.")]
    internal static partial void ToolCallFaulted(ILogger logger, RunId runId, ToolCallId toolCallId, string errorType);

    /// <summary>Logs that the turn limit settled pending tool calls with rejected terminal results instead of invoking them.</summary>
    [LoggerMessage(1035, LogLevel.Information, "Turn limit reached for run {RunId} at turn {TurnId}; {ToolCount} requested tool calls were settled as rejected without invocation.")]
    internal static partial void ToolBatchRejectedAtTurnLimit(ILogger logger, RunId runId, TurnId turnId, int toolCount);

    /// <summary>Logs that the final permitted turn was requested without tools so the model produces its final response.</summary>
    [LoggerMessage(1036, LogLevel.Debug, "Turn {TurnId} of run {RunId} is the final permitted turn ({MaxTurns}); tools are disabled for this request.")]
    internal static partial void FinalTurnToolsDisabled(ILogger logger, RunId runId, TurnId turnId, int maxTurns);

    /// <summary>Logs a model response that could not be accepted as a complete turn because its stop reason or tool-call identities were invalid.</summary>
    [LoggerMessage(1024, LogLevel.Warning, "Model response for run {RunId} turn {TurnId} was not accepted as complete: {Reason}.")]
    internal static partial void ModelResponseNotAccepted(ILogger logger, RunId runId, TurnId turnId, string reason);
}
