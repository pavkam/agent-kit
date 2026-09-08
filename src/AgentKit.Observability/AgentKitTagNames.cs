// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Defines stable structured attribute names shared by AgentKit signals.</summary>
/// <remarks>
/// Domain identity tags are suitable for traces and logs but not ordinary
/// metric dimensions. Content-bearing values are intentionally absent.
/// </remarks>
public static class AgentKitTagNames
{
    /// <summary>Gets the current catalog snapshot version attribute for traces and structured logs.</summary>
    public const string AgentCatalogVersion = "agentkit.agent.catalog.version";

    /// <summary>Gets the tenant identity attribute for traces and structured logs.</summary>
    public const string TenantId = "agentkit.tenant.id";

    /// <summary>Gets the principal identity attribute for traces and structured logs.</summary>
    public const string PrincipalId = "agentkit.principal.id";

    /// <summary>Gets the trusted identity-issuer attribute for traces and structured logs.</summary>
    public const string IdentityIssuer = "agentkit.identity.issuer";

    /// <summary>Gets the OpenTelemetry GenAI operation-name attribute.</summary>
    public const string GenAiOperationName = "gen_ai.operation.name";

    /// <summary>Gets the OpenTelemetry GenAI agent identity attribute.</summary>
    public const string AgentId = "gen_ai.agent.id";

    /// <summary>Gets the OpenTelemetry GenAI conversation identity attribute.</summary>
    public const string ConversationId = "gen_ai.conversation.id";

    /// <summary>Gets the AgentKit session identity attribute.</summary>
    public const string SessionId = "agentkit.session.id";

    /// <summary>Gets the AgentKit run identity attribute.</summary>
    public const string RunId = "agentkit.run.id";

    /// <summary>Gets the AgentKit turn identity attribute.</summary>
    public const string TurnId = "agentkit.turn.id";

    /// <summary>Gets the AgentKit operation identity attribute.</summary>
    public const string OperationId = "agentkit.operation.id";

    /// <summary>Gets the AgentKit model-request identity attribute.</summary>
    public const string ModelRequestId = "agentkit.model_request.id";

    /// <summary>Gets the OpenTelemetry GenAI requested-model attribute.</summary>
    public const string RequestModel = "gen_ai.request.model";

    /// <summary>Gets the OpenTelemetry GenAI provider-name attribute.</summary>
    public const string ProviderName = "gen_ai.provider.name";

    /// <summary>Gets the OpenTelemetry GenAI tool-name attribute.</summary>
    public const string ToolName = "gen_ai.tool.name";

    /// <summary>Gets the AgentKit tool-call identity attribute.</summary>
    public const string ToolCallId = "agentkit.tool_call.id";

    /// <summary>Gets the AgentKit security-request identity attribute.</summary>
    public const string SecurityRequestId = "agentkit.security_request.id";

    /// <summary>Gets the AgentKit security-audit record identity attribute for traces and structured logs.</summary>
    public const string SecurityAuditRecordId = "agentkit.security_audit_record.id";

    /// <summary>Gets the bounded security-audit event-kind attribute.</summary>
    public const string SecurityAuditEventKind = "agentkit.security_audit.event.kind";

    /// <summary>Gets the bounded semantic outcome recorded by a security audit event.</summary>
    public const string SecurityAuditOutcome = "agentkit.security_audit.outcome";

    /// <summary>Gets the normalized protected-operation kind attribute.</summary>
    public const string SecurityOperationKind = "agentkit.security.operation.kind";

    /// <summary>Gets the captured security-authority component key for traces and logs.</summary>
    public const string SecurityAuthorityKey = "agentkit.security.authority.key";

    /// <summary>Gets the selected security-profile key for traces and structured logs.</summary>
    public const string SecurityProfileKey = "agentkit.security.profile.key";

    /// <summary>Gets the normalized protected-effect class attribute.</summary>
    public const string SecurityEffect = "agentkit.security.effect";

    /// <summary>Gets the normalized session operation attribute.</summary>
    public const string SessionOperation = "agentkit.session.operation";

    /// <summary>Gets the normalized budget dimension attribute.</summary>
    public const string BudgetDimension = "agentkit.budget.dimension";

    /// <summary>Gets the budget scope identity attribute.</summary>
    public const string BudgetScopeId = "agentkit.budget.scope.id";

    /// <summary>Gets the stable hook-point identity attribute.</summary>
    public const string HookPoint = "agentkit.hook.point";

    /// <summary>Gets the stable hook-invocation identity attribute.</summary>
    public const string HookInvocationId = "agentkit.hook.invocation.id";

    /// <summary>Gets the logical compaction identity attribute.</summary>
    public const string CompactionId = "agentkit.compaction.id";

    /// <summary>Gets the output-definition identity attribute.</summary>
    public const string OutputDefinitionId = "agentkit.output.definition.id";

    /// <summary>Gets the normalized output mode attribute.</summary>
    public const string OutputMode = "agentkit.output.mode";

    /// <summary>Gets the one-based output-validation attempt attribute.</summary>
    public const string OutputValidationAttempt = "agentkit.output.validation.attempt";

    /// <summary>Gets the bounded local output-schema operation attribute.</summary>
    public const string OutputSchemaOperation = "agentkit.output.schema.operation";

    /// <summary>Gets the normalized file-system operation attribute.</summary>
    public const string FileSystemOperation = "agentkit.filesystem.operation";

    /// <summary>Gets the process-operation identity attribute.</summary>
    public const string ProcessOperationId = "agentkit.process.operation.id";

    /// <summary>Gets the normalized process-stage attribute.</summary>
    public const string ProcessStage = "agentkit.process.stage";

    /// <summary>Gets the negotiated MCP protocol-version attribute.</summary>
    public const string McpProtocolVersion = "agentkit.mcp.protocol.version";

    /// <summary>Gets the immutable MCP catalog-generation attribute.</summary>
    public const string McpCatalogVersion = "agentkit.mcp.catalog.version";

    /// <summary>Gets the bounded MCP lifecycle operation attribute.</summary>
    public const string McpOperation = "agentkit.mcp.operation";

    /// <summary>Gets the immutable model-catalog generation attribute.</summary>
    public const string ModelCatalogVersion = "agentkit.model.catalog.version";

    /// <summary>Gets the language-query identity attribute.</summary>
    public const string LanguageQueryId = "agentkit.language.query.id";

    /// <summary>Gets the bounded language-query kind attribute.</summary>
    public const string LanguageQueryKind = "agentkit.language.query.kind";

    /// <summary>Gets the network-operation identity attribute.</summary>
    public const string NetworkOperationId = "agentkit.network.operation.id";

    /// <summary>Gets the bounded network-stage attribute.</summary>
    public const string NetworkStage = "agentkit.network.stage";

    /// <summary>Gets the implementation type of an observational sink.</summary>
    public const string ObserverName = "agentkit.observer.name";

    /// <summary>Gets the normalized operation outcome attribute.</summary>
    public const string Outcome = "agentkit.outcome";

    /// <summary>Gets the standard normalized error-type attribute.</summary>
    public const string ErrorType = "error.type";

    /// <summary>Gets the one-based agent turn number attribute.</summary>
    public const string TurnNumber = "agentkit.turn.number";

    /// <summary>Gets the bounded continuation-boundary kind attribute.</summary>
    public const string ContinuationBoundary = "agentkit.continuation.boundary";

    /// <summary>Gets the bounded continuation-decision kind attribute.</summary>
    public const string ContinuationDecision = "agentkit.continuation.decision";

    /// <summary>Gets the bounded selected continuation-cause kind attribute.</summary>
    public const string ContinuationReason = "agentkit.continuation.reason";

    /// <summary>Gets the bounded input-promotion boundary attribute.</summary>
    public const string InputPromotionBoundary = "agentkit.input.promotion.boundary";

    /// <summary>Gets the execution-lane identity correlation attribute for traces and logs.</summary>
    public const string ExecutionLaneId = "agentkit.execution.lane.id";
}
