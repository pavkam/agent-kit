// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Source-generated, content-free log events for the delegation worker.</summary>
internal static partial class GoalWorkerLog
{
    /// <summary>Logs that a child attempt was claimed and is about to run.</summary>
    [LoggerMessage(EventId = 31400, Level = LogLevel.Information, Message = "Worker claimed attempt {AttemptNumber} of child goal {GoalId} for agent {AgentId}.")]
    internal static partial void AttemptClaimed(ILogger logger, GoalId goalId, int attemptNumber, AgentId agentId);

    /// <summary>Logs that a child settled.</summary>
    [LoggerMessage(EventId = 31401, Level = LogLevel.Information, Message = "Worker settled child goal {GoalId} as {Status}.")]
    internal static partial void ChildSettled(ILogger logger, GoalId goalId, DelegationStatus status);

    /// <summary>Logs that an intent was skipped.</summary>
    [LoggerMessage(EventId = 31402, Level = LogLevel.Debug, Message = "Worker skipped child goal {GoalId}: {Reason}.")]
    internal static partial void IntentSkipped(ILogger logger, GoalId goalId, string reason);

    /// <summary>Logs recovery of an attempt left running by a previous incarnation.</summary>
    [LoggerMessage(EventId = 31403, Level = LogLevel.Warning, Message = "Worker settled stale running attempt of child goal {GoalId} as failed with unknown effects.")]
    internal static partial void StaleAttemptRecovered(ILogger logger, GoalId goalId);

    /// <summary>Logs that draining one intent faulted.</summary>
    [LoggerMessage(EventId = 31404, Level = LogLevel.Error, Message = "Worker failed while draining child goal {GoalId} with {ErrorType}.")]
    internal static partial void DrainFaulted(ILogger logger, GoalId goalId, string errorType);

    /// <summary>Logs that a durable intent scan failed.</summary>
    [LoggerMessage(EventId = 31405, Level = LogLevel.Warning, Message = "Worker intent scan for profile {ProfileKey} failed: {Reason}.")]
    internal static partial void ScanFailed(ILogger logger, GoalProfileKey profileKey, string reason);

    /// <summary>Logs that the worker started.</summary>
    [LoggerMessage(EventId = 31406, Level = LogLevel.Information, Message = "Goal delegation worker started with {Slots} slots and {Profiles} scanned profiles.")]
    internal static partial void WorkerStarted(ILogger logger, int slots, int profiles);

    /// <summary>Logs that the worker stopped.</summary>
    [LoggerMessage(EventId = 31407, Level = LogLevel.Information, Message = "Goal delegation worker stopped.")]
    internal static partial void WorkerStopped(ILogger logger);

    /// <summary>Logs that the engine did not host a child's target.</summary>
    [LoggerMessage(EventId = 31408, Level = LogLevel.Warning, Message = "Delegation {DelegationId} targets agent {AgentId}, which this engine does not host.")]
    internal static partial void TargetUnknown(ILogger logger, DelegationId delegationId, AgentId agentId);

    /// <summary>Logs that the engine refused to admit a child run.</summary>
    [LoggerMessage(EventId = 31409, Level = LogLevel.Warning, Message = "Delegation {DelegationId} child run was not admitted ({ErrorType}).")]
    internal static partial void RunRejected(ILogger logger, DelegationId delegationId, string errorType);

    /// <summary>Logs that a message was admitted.</summary>
    [LoggerMessage(EventId = 31410, Level = LogLevel.Information, Message = "Message from agent {SenderAgentId} admitted to session {SessionId}; existing {Existing}.")]
    internal static partial void MessageAdmitted(ILogger logger, AgentId senderAgentId, SessionId sessionId, bool existing);

    /// <summary>Logs that a message was not admitted.</summary>
    [LoggerMessage(EventId = 31411, Level = LogLevel.Warning, Message = "Message from agent {SenderAgentId} to session {SessionId} was rejected: {Kind}.")]
    internal static partial void MessageRejected(ILogger logger, AgentId senderAgentId, SessionId sessionId, AgentMessageRejectionKind kind);
}
