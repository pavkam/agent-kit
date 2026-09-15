// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Defines content-free structured logs emitted by the human-question publication boundary.</summary>
internal static partial class HumanQuestionBrokerLog
{
    /// <summary>Records that a broker reached a bounded terminal publication state.</summary>
    /// <param name="logger">The non-null logger receiving structural fields only.</param><param name="questionId">The stable question identity.</param><param name="tenantId">The established tenant identity.</param><param name="agentId">The established agent identity.</param><param name="sessionId">The established session identity when present.</param><param name="runId">The established active run identity.</param><param name="turnId">The established turn identity when the operation is turn-scoped.</param><param name="toolCallId">The established calling tool identity.</param><param name="operationId">The established causal operation identity.</param><param name="securityRequestId">The established permission-request identity.</param><param name="level">Error for a failed outcome and Information otherwise.</param><param name="outcome">The bounded terminal outcome.</param>
    [LoggerMessage(EventId = 22003, Message = "Human question {QuestionId} publication completed with {Outcome}. Tenant {TenantId} agent {AgentId} session {SessionId} run {RunId} turn {TurnId} tool {ToolCallId} operation {OperationId} security request {SecurityRequestId}.")]
    internal static partial void Completed(ILogger logger, QuestionId questionId, TenantId tenantId, AgentId agentId,
        SessionId? sessionId, RunId? runId, TurnId? turnId, ToolCallId toolCallId, OperationId operationId,
        SecurityRequestId securityRequestId, LogLevel level, string outcome);

    /// <summary>Records caller cancellation without asserting that an already-started presentation was revoked.</summary>
    /// <param name="logger">The non-null logger receiving structural fields only.</param><param name="questionId">The stable question identity.</param><param name="tenantId">The established tenant identity.</param><param name="agentId">The established agent identity.</param><param name="sessionId">The established session identity when present.</param><param name="runId">The established active run identity.</param><param name="turnId">The established turn identity when the operation is turn-scoped.</param><param name="toolCallId">The established calling tool identity.</param><param name="operationId">The established causal operation identity.</param><param name="securityRequestId">The established permission-request identity.</param>
    [LoggerMessage(EventId = 22004, Level = LogLevel.Information, Message = "Human question {QuestionId} publication wait was cancelled. Tenant {TenantId} agent {AgentId} session {SessionId} run {RunId} turn {TurnId} tool {ToolCallId} operation {OperationId} security request {SecurityRequestId}.")]
    internal static partial void Cancelled(ILogger logger, QuestionId questionId, TenantId tenantId, AgentId agentId,
        SessionId? sessionId, RunId? runId, TurnId? turnId, ToolCallId toolCallId, OperationId operationId,
        SecurityRequestId securityRequestId);

    /// <summary>Records an unexpected broker failure using only its type and established identities, never content.</summary>
    /// <param name="logger">The non-null logger receiving structural fields only.</param><param name="questionId">The stable question identity.</param><param name="tenantId">The established tenant identity.</param><param name="agentId">The established agent identity.</param><param name="sessionId">The established session identity when present.</param><param name="runId">The established active run identity.</param><param name="turnId">The established turn identity when the operation is turn-scoped.</param><param name="toolCallId">The established calling tool identity.</param><param name="operationId">The established causal operation identity.</param><param name="securityRequestId">The established permission-request identity.</param><param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 22005, Level = LogLevel.Error, Message = "Human question {QuestionId} publication failed with error type {ErrorType}. Tenant {TenantId} agent {AgentId} session {SessionId} run {RunId} turn {TurnId} tool {ToolCallId} operation {OperationId} security request {SecurityRequestId}.")]
    internal static partial void Failed(ILogger logger, QuestionId questionId, TenantId tenantId, AgentId agentId,
        SessionId? sessionId, RunId? runId, TurnId? turnId, ToolCallId toolCallId, OperationId operationId,
        SecurityRequestId securityRequestId, string errorType);
}
