// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Defines content-free structured logs emitted by the protected task-delegation dispatch boundary.</summary>
internal static partial class TaskDelegationBrokerLog
{
    /// <summary>Records that a broker reached a bounded terminal dispatch state.</summary>
    /// <param name="logger">The non-null logger receiving structural fields only.</param><param name="delegationId">The stable delegation identity.</param><param name="tenantId">The established tenant identity.</param><param name="agentId">The established parent agent identity.</param><param name="sessionId">The established parent session identity.</param><param name="runId">The established parent run identity.</param><param name="turnId">The established turn identity when the operation is turn-scoped.</param><param name="toolCallId">The established calling tool identity.</param><param name="operationId">The established causal operation identity.</param><param name="securityRequestId">The established permission-request identity.</param><param name="level">Error for a failed outcome and Information otherwise.</param><param name="outcome">The bounded terminal outcome.</param>
    [LoggerMessage(EventId = 23000, Message = "Task delegation {DelegationId} dispatch completed with {Outcome}. Tenant {TenantId} agent {AgentId} session {SessionId} run {RunId} turn {TurnId} tool {ToolCallId} operation {OperationId} security request {SecurityRequestId}.")]
    internal static partial void Completed(ILogger logger, DelegationId delegationId, TenantId tenantId, AgentId agentId,
        SessionId sessionId, RunId runId, TurnId? turnId, ToolCallId toolCallId, OperationId operationId,
        SecurityRequestId securityRequestId, LogLevel level, string outcome);

    /// <summary>Records caller cancellation without asserting that an already-started child was cancelled or revoked.</summary>
    /// <param name="logger">The non-null logger receiving structural fields only.</param><param name="delegationId">The stable delegation identity.</param><param name="tenantId">The established tenant identity.</param><param name="agentId">The established parent agent identity.</param><param name="sessionId">The established parent session identity.</param><param name="runId">The established parent run identity.</param><param name="turnId">The established turn identity when the operation is turn-scoped.</param><param name="toolCallId">The established calling tool identity.</param><param name="operationId">The established causal operation identity.</param><param name="securityRequestId">The established permission-request identity.</param>
    [LoggerMessage(EventId = 23001, Level = LogLevel.Information, Message = "Task delegation {DelegationId} dispatch wait was cancelled. Tenant {TenantId} agent {AgentId} session {SessionId} run {RunId} turn {TurnId} tool {ToolCallId} operation {OperationId} security request {SecurityRequestId}.")]
    internal static partial void Cancelled(ILogger logger, DelegationId delegationId, TenantId tenantId, AgentId agentId,
        SessionId sessionId, RunId runId, TurnId? turnId, ToolCallId toolCallId, OperationId operationId,
        SecurityRequestId securityRequestId);

    /// <summary>Records an unexpected dispatch failure using only its type and established identities, never content.</summary>
    /// <param name="logger">The non-null logger receiving structural fields only.</param><param name="delegationId">The stable delegation identity.</param><param name="tenantId">The established tenant identity.</param><param name="agentId">The established parent agent identity.</param><param name="sessionId">The established parent session identity.</param><param name="runId">The established parent run identity.</param><param name="turnId">The established turn identity when the operation is turn-scoped.</param><param name="toolCallId">The established calling tool identity.</param><param name="operationId">The established causal operation identity.</param><param name="securityRequestId">The established permission-request identity.</param><param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 23002, Level = LogLevel.Error, Message = "Task delegation {DelegationId} dispatch failed with error type {ErrorType}. Tenant {TenantId} agent {AgentId} session {SessionId} run {RunId} turn {TurnId} tool {ToolCallId} operation {OperationId} security request {SecurityRequestId}.")]
    internal static partial void Failed(ILogger logger, DelegationId delegationId, TenantId tenantId, AgentId agentId,
        SessionId sessionId, RunId runId, TurnId? turnId, ToolCallId toolCallId, OperationId operationId,
        SecurityRequestId securityRequestId, string errorType);
}
