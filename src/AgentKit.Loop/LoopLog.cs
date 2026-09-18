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

    /// <summary>Logs a required terminal commit that did not complete within the configured settlement bound.</summary>
    /// <remarks>Whether the commit landed is unknown; the run settles as a session operation failure saying so rather than hanging.</remarks>
    [LoggerMessage(1043, LogLevel.Error, "Settlement commit for run {RunId} in session {SessionId} did not complete within {SettlementTimeout}; the commit outcome is unknown.")]
    internal static partial void SettlementTimedOut(ILogger logger, RunId runId, SessionId sessionId, TimeSpan settlementTimeout);

    /// <summary>Logs a required terminal commit the store reported as failed being retried under its unchanged idempotency key.</summary>
    [LoggerMessage(1044, LogLevel.Warning, "Settlement commit for run {RunId} in session {SessionId} failed on attempt {Attempt}; retrying after {Delay} under the same idempotency key.")]
    internal static partial void SettlementCommitRetryScheduled(ILogger logger, RunId runId, SessionId sessionId, int attempt, TimeSpan delay);

    /// <summary>Logs run-start recovery settling tool calls a previous run left without terminal results.</summary>
    /// <remarks>The settlement appends interrupted results with unknown side-effect certainty and never invokes a tool.</remarks>
    [LoggerMessage(1090, LogLevel.Warning, "Run {RunId} found {ToolCount} tool calls left without a terminal result by a previous run; settling them as interrupted before the first turn.")]
    internal static partial void DanglingToolCallsSettled(ILogger logger, RunId runId, int toolCount);

    /// <summary>Logs that a run's model-facing history was reconstructed from the newest active compaction checkpoint rather than from the branch origin.</summary>
    /// <remarks>Counts and identities only: the checkpoint summary never enters the log. The covered count is the number of loaded entries omitted because they precede the checkpoint's retained suffix; the retained count is the number of messages projected after the summary.</remarks>
    [LoggerMessage(
        1091,
        LogLevel.Information,
        "Run {RunId} reconstructed history from active compaction checkpoint {CompactionId} at sequence {CheckpointSequence}: " +
            "{CoveredEntryCount} covered entries omitted, {RetainedMessageCount} retained messages follow the summary.")]
    internal static partial void HistoryReconstructedFromCompactionCheckpoint(
        ILogger logger,
        RunId runId,
        CompactionId compactionId,
        SessionSequence checkpointSequence,
        int coveredEntryCount,
        int retainedMessageCount);

    /// <summary>Logs a newer active compaction checkpoint whose covered range does not include an older active checkpoint on the same branch.</summary>
    /// <remarks>The first-party compactor always covers a contiguous prefix, so this indicates a foreign or inconsistent record; the newest checkpoint is still the one used.</remarks>
    [LoggerMessage(
        1092,
        LogLevel.Warning,
        "Run {RunId} found active compaction checkpoint {CompactionId} at sequence {CheckpointSequence} whose covered range ends at " +
            "{CoveredEndSequence}, before the older active checkpoint {OlderCompactionId} at sequence {OlderCheckpointSequence}; the newest checkpoint is used.")]
    internal static partial void OlderCompactionCheckpointNotCovered(
        ILogger logger,
        RunId runId,
        CompactionId compactionId,
        SessionSequence checkpointSequence,
        SessionSequence coveredEndSequence,
        CompactionId olderCompactionId,
        SessionSequence olderCheckpointSequence);

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

    /// <summary>Logs that the run selects an output definition but no output processor was composed, so it fails closed.</summary>
    [LoggerMessage(1091, LogLevel.Error, "Run {RunId} turn {TurnId} selects output definition {OutputDefinitionId} but no output processor is composed; the run fails closed.")]
    internal static partial void OutputProcessorMissing(ILogger logger, RunId runId, TurnId turnId, OutputDefinitionId outputDefinitionId);

    /// <summary>Logs the output processor's typed decision for one terminal response.</summary>
    [LoggerMessage(1092, LogLevel.Information, "Run {RunId} turn {TurnId} output definition {OutputDefinitionId} attempt {Attempt} decided {Decision}.")]
    internal static partial void OutputDecided(ILogger logger, RunId runId, TurnId turnId, OutputDefinitionId outputDefinitionId, int attempt, string decision);

    /// <summary>Logs that output validation was cancelled after the response was committed.</summary>
    [LoggerMessage(1093, LogLevel.Information, "Run {RunId} turn {TurnId} output validation for {OutputDefinitionId} attempt {Attempt} was cancelled; the response is committed.")]
    internal static partial void OutputValidationCancelled(ILogger logger, RunId runId, TurnId turnId, OutputDefinitionId outputDefinitionId, int attempt);

    /// <summary>Logs that the output processor threw instead of returning a typed decision.</summary>
    [LoggerMessage(1094, LogLevel.Error, "Run {RunId} turn {TurnId} output validation for {OutputDefinitionId} attempt {Attempt} faulted with {ErrorType}.")]
    internal static partial void OutputValidationFaulted(ILogger logger, RunId runId, TurnId turnId, OutputDefinitionId outputDefinitionId, int attempt, string errorType);

    /// <summary>Logs that a transform hook point failed and the turn settled without sending the request.</summary>
    [LoggerMessage(1095, LogLevel.Error, "Run {RunId} turn {TurnId}: hook point {HookPoint} failed with {ErrorType}; the turn did not proceed.")]
    internal static partial void HookFailedTurn(ILogger logger, RunId runId, TurnId turnId, HookPointId hookPoint, string errorType);

    /// <summary>Logs that a before-tool-invocation hook vetoed a call, which settled as rejected without invocation.</summary>
    [LoggerMessage(1096, LogLevel.Information, "Run {RunId}: tool call {ToolCallId} was vetoed by a hook and settled as rejected without invocation.")]
    internal static partial void ToolCallVetoed(ILogger logger, RunId runId, ToolCallId toolCallId);

    /// <summary>Logs that estimated history size crossed the pressure threshold and a compaction was requested.</summary>
    [LoggerMessage(1097, LogLevel.Information, "Run {RunId}: history estimated at {EstimatedTokens} tokens against a {ContextWindow}-token window; requesting compaction {CompactionId}.")]
    internal static partial void CompactionTriggered(ILogger logger, RunId runId, CompactionId compactionId, long estimatedTokens, long contextWindow);

    /// <summary>Logs that a requested compaction did not yield a checkpoint and the run continues uncompacted.</summary>
    [LoggerMessage(1098, LogLevel.Warning, "Run {RunId}: compaction {CompactionId} was not applied ({Outcome}); continuing with the current history.")]
    internal static partial void CompactionNotApplied(ILogger logger, RunId runId, CompactionId compactionId, string outcome);

    /// <summary>Logs that the compactor threw; the run continues uncompacted.</summary>
    [LoggerMessage(1099, LogLevel.Error, "Run {RunId}: compaction {CompactionId} faulted with {ErrorType}; continuing with the current history.")]
    internal static partial void CompactionFaulted(ILogger logger, RunId runId, CompactionId compactionId, string errorType);

    /// <summary>Logs that a checkpoint was activated and the model-facing history was rebuilt from it.</summary>
    [LoggerMessage(1100, LogLevel.Information, "Run {RunId}: compaction {CompactionId} applied; model-facing history went from {MessagesBefore} to {MessagesAfter} messages.")]
    internal static partial void CompactionApplied(ILogger logger, RunId runId, CompactionId compactionId, int messagesBefore, int messagesAfter);

    /// <summary>Logs that a budgeted run found no budget authority and failed closed.</summary>
    [LoggerMessage(1101, LogLevel.Error, "Run {RunId} declares budget limits but no budget authority is composed; the run fails closed.")]
    internal static partial void BudgetAuthorityMissing(ILogger logger, RunId runId);

    /// <summary>Logs that the run's budget scope could not be created.</summary>
    [LoggerMessage(1102, LogLevel.Error, "Run {RunId}: the budget scope could not be created ({Outcome}); the run fails closed.")]
    internal static partial void BudgetScopeNotCreated(ILogger logger, RunId runId, string outcome);

    /// <summary>Logs that a reservation was refused and the run or call stopped.</summary>
    [LoggerMessage(1103, LogLevel.Warning, "Run {RunId}: the {Dimension} budget is exhausted; no further work on it is attempted.")]
    internal static partial void BudgetExhausted(ILogger logger, RunId runId, BudgetDimension dimension);

    /// <summary>Logs that fresh authorization for a durably admitted run's lane release could not be captured.</summary>
    [LoggerMessage(1104, LogLevel.Warning, "Run {RunId}: authorization for releasing the admitted lane could not be captured; the lane was not released.")]
    internal static partial void LaneReleaseAuthorizationUnavailable(ILogger logger, RunId runId);

    /// <summary>Logs that releasing a durably admitted run's lane was rejected.</summary>
    [LoggerMessage(1105, LogLevel.Warning, "Run {RunId}: releasing the admitted lane was rejected ({Outcome}); the lane was not released.")]
    internal static partial void LaneReleaseRejected(ILogger logger, RunId runId, string outcome);

    /// <summary>Logs that releasing a durably admitted run's lane faulted.</summary>
    [LoggerMessage(1106, LogLevel.Error, "Run {RunId}: releasing the admitted lane faulted with {ErrorType}; the lane was not released.")]
    internal static partial void LaneReleaseFaulted(ILogger logger, RunId runId, string errorType);
}
