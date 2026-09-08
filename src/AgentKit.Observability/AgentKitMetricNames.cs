// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Defines stable names for bounded metric instruments emitted by AgentKit.</summary>
/// <remarks>Metric names use lowercase dotted namespaces and never embed identities.</remarks>
public static class AgentKitMetricNames
{
    /// <summary>Gets the counter for terminal pinned-agent admission outcomes.</summary>
    public const string AgentAdmissionCount = "agentkit.agent.admission.count";

    /// <summary>Gets the counter for terminal identity-resolution outcomes.</summary>
    public const string IdentityResolutionCount = "agentkit.identity.resolution.count";

    /// <summary>Gets the counter for terminal identity-derivation outcomes.</summary>
    public const string IdentityDerivationCount = "agentkit.identity.derivation.count";

    /// <summary>Gets the counter for terminal agent-run outcomes.</summary>
    public const string AgentRunCount = "agentkit.agent.run.count";

    /// <summary>Gets the histogram for settled agent-run duration in seconds.</summary>
    public const string AgentRunDuration = "agentkit.agent.run.duration";

    /// <summary>Gets the counter for terminal continuation-policy evaluations.</summary>
    public const string RunContinuationEvaluationCount = "agentkit.run.continuation.evaluation.count";

    /// <summary>Gets the histogram for continuation-policy evaluation duration in seconds.</summary>
    public const string RunContinuationEvaluationDuration = "agentkit.run.continuation.evaluation.duration";

    /// <summary>Gets the counter for terminal context-preparation outcomes.</summary>
    public const string ContextPreparationCount = "agentkit.context.prepare.count";

    /// <summary>Gets the histogram for context-preparation duration in seconds.</summary>
    public const string ContextPreparationDuration = "agentkit.context.prepare.duration";

    /// <summary>Gets the counter for terminal tool-call outcomes.</summary>
    public const string ToolCallCount = "agentkit.tool.call.count";

    /// <summary>Gets the histogram for tool-call duration in seconds.</summary>
    public const string ToolCallDuration = "agentkit.tool.call.duration";

    /// <summary>Gets the counter for terminal security authorization decisions.</summary>
    public const string SecurityDecisionCount = "agentkit.security.decision.count";

    /// <summary>Gets the counter for terminal security-profile capture outcomes.</summary>
    public const string SecurityProfileCaptureCount = "agentkit.security.profile.capture.count";

    /// <summary>Gets the histogram for security-profile capture duration in seconds.</summary>
    public const string SecurityProfileCaptureDuration = "agentkit.security.profile.capture.duration";

    /// <summary>Gets the counter for exact security-profile publication-read outcomes.</summary>
    public const string SecurityProfilePublicationReadCount = "agentkit.security.profile.publication.read.count";

    /// <summary>Gets the counter for terminal captured security-authority selection outcomes.</summary>
    public const string SecurityAuthoritySelectionCount = "agentkit.security.authority.selection.count";

    /// <summary>Gets the histogram for captured security-authority selection duration in seconds.</summary>
    public const string SecurityAuthoritySelectionDuration = "agentkit.security.authority.selection.duration";

    /// <summary>Gets the counter for terminal security-audit dispatch outcomes.</summary>
    public const string SecurityAuditDispatchCount = "agentkit.security.audit.dispatch.count";

    /// <summary>Gets the histogram for security-audit dispatch duration in seconds.</summary>
    public const string SecurityAuditDispatchDuration = "agentkit.security.audit.dispatch.duration";

    /// <summary>Gets the counter for terminal session coordination outcomes.</summary>
    public const string SessionOperationCount = "agentkit.session.operation.count";

    /// <summary>Gets the counter for terminal concrete session-store outcomes.</summary>
    public const string SessionStoreOperationCount = "agentkit.session.store.operation.count";

    /// <summary>Gets the counter for terminal budget reservation outcomes.</summary>
    public const string BudgetReservationCount = "agentkit.budget.reservation.count";

    /// <summary>Gets the counter for terminal budget settlement outcomes.</summary>
    public const string BudgetSettlementCount = "agentkit.budget.settlement.count";

    /// <summary>Gets the counter for terminal budget start-accounting outcomes.</summary>
    public const string BudgetStartCount = "agentkit.budget.start.count";

    /// <summary>Gets the counter for terminal budget correction outcomes.</summary>
    public const string BudgetCorrectionCount = "agentkit.budget.correction.count";

    /// <summary>Gets the counter for terminal hook dispatch outcomes.</summary>
    public const string HookDispatchCount = "agentkit.hook.dispatch.count";

    /// <summary>Gets the counter for terminal context-compaction outcomes.</summary>
    public const string ContextCompactionCount = "agentkit.context.compaction.count";

    /// <summary>Gets the counter for terminal output-processing decisions.</summary>
    public const string OutputProcessingCount = "agentkit.output.processing.count";

    /// <summary>Gets the count of bounded local output-schema operations.</summary>
    public const string OutputSchemaOperationCount = "agentkit.output.schema.operation.count";

    /// <summary>Gets the counter for terminal input-promotion planning outcomes.</summary>
    public const string InputPromotionPlanCount = "agentkit.input.promotion.plan.count";

    /// <summary>Gets the histogram for input-promotion planning duration in seconds.</summary>
    public const string InputPromotionPlanDuration = "agentkit.input.promotion.plan.duration";

    /// <summary>Gets the counter for terminal file-system host operations.</summary>
    public const string FileSystemOperationCount = "agentkit.filesystem.operation.count";

    /// <summary>Gets the counter for terminal process-stage outcomes.</summary>
    public const string ProcessOperationCount = "agentkit.process.operation.count";

    /// <summary>Gets the counter for terminal MCP client-operation outcomes.</summary>
    public const string McpClientOperationCount = "agentkit.mcp.client.operation.count";

    /// <summary>Gets the counter for terminal model-catalog refresh outcomes.</summary>
    public const string ModelCatalogRefreshCount = "agentkit.model.catalog.refresh.count";

    /// <summary>Gets the counter for terminal model-selection outcomes.</summary>
    public const string ModelSelectionCount = "agentkit.model.selection.count";

    /// <summary>Gets the counter for terminal language-query outcomes.</summary>
    public const string LanguageQueryCount = "agentkit.language.query.count";

    /// <summary>Gets the counter for terminal network boundary outcomes.</summary>
    public const string NetworkOperationCount = "agentkit.network.operation.count";
}
