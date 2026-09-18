// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Source-generated, content-free log events for <see cref="EngineDelegationChannel"/>.</summary>
internal static partial class EngineDelegationLog
{
    /// <summary>Logs that a delegation named an agent the engine does not host.</summary>
    [LoggerMessage(18100, LogLevel.Warning, "Delegation {DelegationId} targets agent {AgentId}, which this engine does not host; rejected.")]
    internal static partial void TargetUnknown(ILogger logger, DelegationId delegationId, AgentId agentId);

    /// <summary>Logs that the deadline elapsed before the child turn completed.</summary>
    [LoggerMessage(18101, LogLevel.Information, "Delegation {DelegationId} to agent {AgentId} reached its deadline before the child turn completed.")]
    internal static partial void DeadlineElapsed(ILogger logger, DelegationId delegationId, AgentId agentId);

    /// <summary>Logs that the child turn was rejected at admission.</summary>
    [LoggerMessage(18102, LogLevel.Warning, "Delegation {DelegationId} to agent {AgentId} was rejected at admission ({ErrorType}).")]
    internal static partial void ChildRejected(ILogger logger, DelegationId delegationId, AgentId agentId, string errorType);

    /// <summary>Logs the child turn's settlement.</summary>
    [LoggerMessage(18103, LogLevel.Information, "Delegation {DelegationId} to agent {AgentId} settled in session {SessionId} run {RunId} as {Status}.")]
    internal static partial void ChildSettled(ILogger logger, DelegationId delegationId, AgentId agentId, SessionId sessionId, RunId runId, TaskDelegationStatus status);
}
