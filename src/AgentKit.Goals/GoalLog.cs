// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Defines content-free structured logs emitted by the goal and delegation runtime.</summary>
/// <remarks>Event identifiers 31000-31099 are owned by this package. Logs carry identities and bounded vocabularies only, never objectives, results, prompts, or tool arguments.</remarks>
internal static partial class GoalLog
{
    /// <summary>Records a durably created goal.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="goalId">The goal.</param>
    /// <param name="parentGoalId">The parent, when present.</param>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="replayed">Whether the creation was an idempotent replay.</param>
    [LoggerMessage(EventId = 31000, Level = LogLevel.Information, Message = "Goal {GoalId} (parent {ParentGoalId}) created for agent {AgentId} session {SessionId}; replayed {Replayed}.")]
    internal static partial void GoalCreated(ILogger logger, GoalId goalId, GoalId? parentGoalId, AgentId agentId, SessionId sessionId, bool replayed);

    /// <summary>Records a committed goal transition.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="goalId">The goal.</param>
    /// <param name="from">The prior status.</param>
    /// <param name="to">The new status.</param>
    /// <param name="replayed">Whether the transition was an idempotent replay.</param>
    [LoggerMessage(EventId = 31001, Level = LogLevel.Information, Message = "Goal {GoalId} moved {From} to {To}; replayed {Replayed}.")]
    internal static partial void GoalTransitioned(ILogger logger, GoalId goalId, GoalStatus from, GoalStatus to, bool replayed);

    /// <summary>Records a goal operation the store refused.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="kind">The bounded failure class.</param>
    [LoggerMessage(EventId = 31002, Level = LogLevel.Warning, Message = "Goal operation {Operation} was refused: {Kind}.")]
    internal static partial void GoalOperationRefused(ILogger logger, string operation, GoalStoreFailureKind kind);

    /// <summary>Records a delegation refused before any child existed.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="delegationId">The delegation.</param>
    /// <param name="kind">The bounded rejection class.</param>
    [LoggerMessage(EventId = 31003, Level = LogLevel.Warning, Message = "Delegation {DelegationId} was rejected: {Kind}.")]
    internal static partial void DelegationRejected(ILogger logger, DelegationId delegationId, DelegationRejectionKind kind);

    /// <summary>Records a durably committed child-admission intent.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="delegationId">The delegation.</param>
    /// <param name="childGoalId">The child goal.</param>
    /// <param name="targetAgentId">The target agent.</param>
    [LoggerMessage(EventId = 31004, Level = LogLevel.Information, Message = "Delegation {DelegationId} dispatched child goal {ChildGoalId} to agent {TargetAgentId}.")]
    internal static partial void DelegationDispatched(ILogger logger, DelegationId delegationId, GoalId childGoalId, AgentId targetAgentId);

    /// <summary>Records a delegation wait that ended.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="delegationId">The delegation.</param>
    /// <param name="status">The reported child status.</param>
    [LoggerMessage(EventId = 31005, Level = LogLevel.Information, Message = "Delegation {DelegationId} ended with child status {Status}.")]
    internal static partial void DelegationSettled(ILogger logger, DelegationId delegationId, DelegationStatus status);

    /// <summary>Records a join decision.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="goalId">The parent goal.</param>
    /// <param name="strategy">The strategy key.</param>
    /// <param name="decision">The bounded decision kind.</param>
    [LoggerMessage(EventId = 31006, Level = LogLevel.Information, Message = "Join for goal {GoalId} under strategy {Strategy} decided {Decision}.")]
    internal static partial void JoinDecided(ILogger logger, GoalId goalId, string strategy, string decision);

    /// <summary>Records an event sink that was missing.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="sinkId">The sink identity.</param>
    /// <param name="required">Whether the sink was required.</param>
    [LoggerMessage(EventId = 31007, Level = LogLevel.Warning, Message = "Goal event sink {SinkId} is unavailable; required {Required}.")]
    internal static partial void EventSinkUnavailable(ILogger logger, string sinkId, bool required);

    /// <summary>Records an event sink that failed.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="sinkId">The sink identity.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(EventId = 31008, Level = LogLevel.Warning, Message = "Goal event sink {SinkId} failed with {ErrorType}.")]
    internal static partial void EventSinkFailed(ILogger logger, string sinkId, string errorType);

    /// <summary>Records an unexpected goal or delegation failure by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(EventId = 31009, Level = LogLevel.Error, Message = "Goal operation {Operation} failed with {ErrorType}.")]
    internal static partial void OperationFailed(ILogger logger, string operation, string errorType);

    /// <summary>Records a run-root goal materialized for a delegating run.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="goalId">The materialized root goal.</param>
    /// <param name="runId">The run it represents.</param>
    [LoggerMessage(EventId = 31010, Level = LogLevel.Information, Message = "Materialized root goal {GoalId} for run {RunId}.")]
    internal static partial void RootGoalMaterialized(ILogger logger, GoalId goalId, RunId runId);
}
