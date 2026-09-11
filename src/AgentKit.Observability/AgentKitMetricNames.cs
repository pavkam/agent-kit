// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Defines stable names for bounded metric instruments emitted by AgentKit.</summary>
/// <remarks>Metric names use lowercase dotted namespaces and never embed identities.</remarks>
public static class AgentKitMetricNames
{
    /// <summary>Counts completed catalog merge and merge-policy operations using bounded operation and outcome dimensions.</summary>
    public const string ToolCatalogMergeCount = "agentkit.tool.catalog.merge.count";
    /// <summary>Measures elapsed seconds for catalog merge and merge-policy operations with a valid diagnostic clock.</summary>
    public const string ToolCatalogMergeDuration = "agentkit.tool.catalog.merge.duration";
    /// <summary>Counts completed tool-source discovery operations using bounded outcome dimensions.</summary>
    public const string ToolProviderDiscoveryCount = "agentkit.tool.provider.discovery.count";
    /// <summary>Measures tool-source discovery duration in seconds when the diagnostic clock is available.</summary>
    public const string ToolProviderDiscoveryDuration = "agentkit.tool.provider.discovery.duration";

    /// <summary>Counts completed catalog-capture operations with bounded operation and outcome dimensions.</summary>
    public const string ToolCatalogCaptureOperationCount = "agentkit.tool.catalog.capture.operation.count";
    /// <summary>Measures catalog-capture elapsed seconds when the diagnostic clock is available.</summary>
    public const string ToolCatalogCaptureOperationDuration = "agentkit.tool.catalog.capture.operation.duration";

    /// <summary>Counts completed invoker acquisition, release, capture closure, and owned cleanup operations.</summary>
    /// <remarks>Dimensions are restricted to the bounded operation and outcome vocabularies.</remarks>
    public const string ToolProviderCaptureOperationCount = "agentkit.tool.provider.capture.operation.count";

    /// <summary>Measures elapsed seconds for retained source-capture operations.</summary>
    /// <remarks>Unavailable or negative clock measurements are omitted; identities and content are never dimensions.</remarks>
    public const string ToolProviderCaptureOperationDuration = "agentkit.tool.provider.capture.operation.duration";


    /// <summary>Gets the histogram for session-entry codec operation duration in seconds.</summary>
    public const string SessionEntryCodecDuration = "agentkit.session.entry.codec.duration";
    /// <summary>Gets the counter for terminal session-entry codec outcomes.</summary>
    public const string SessionEntryCodecCount = "agentkit.session.entry.codec.count";
    /// <summary>Gets the counter for terminal AgentKit service-provider build outcomes.</summary>
    public const string AgentCompositionBuildCount = "agentkit.agent.composition.build.count";

    /// <summary>Gets the histogram for AgentKit service-provider build duration in seconds.</summary>
    public const string AgentCompositionBuildDuration = "agentkit.agent.composition.build.duration";

    /// <summary>Gets the counter for terminal pinned-agent admission outcomes.</summary>
    public const string AgentAdmissionCount = "agentkit.agent.admission.count";

    /// <summary>Gets the counter for exact run-profile publication reads.</summary>
    public const string AgentRunProfilePublicationReadCount = "agentkit.agent.run_profile.read.count";

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

    /// <summary>Counts exact projection-policy lookups by bounded terminal outcome.</summary>
    /// <remarks>The counter uses resolution units and excludes policy identity and content from its dimensions.</remarks>
    public const string ToolResultProjectionPolicyResolutionCount = "agentkit.tool.result.projection_policy.resolution.count";

    /// <summary>Measures exact projection-policy lookup duration in seconds by bounded terminal outcome.</summary>
    /// <remarks>The histogram records only valid measured durations; clock failures never become reported zero.</remarks>
    public const string ToolResultProjectionPolicyResolutionDuration = "agentkit.tool.result.projection_policy.resolution.duration";

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

    /// <summary>Gets the counter for terminal grant-consumption outcomes.</summary>
    public const string SecurityGrantConsumptionCount = "agentkit.security.grant.consumption.count";

    /// <summary>Gets the counter for terminal authoritative security-grant store operations.</summary>
    public const string SecurityGrantStoreOperationCount = "agentkit.security.grant.store.operation.count";

    /// <summary>Gets the histogram for authoritative security-grant store operation duration in seconds.</summary>
    public const string SecurityGrantStoreOperationDuration = "agentkit.security.grant.store.operation.duration";

    /// <summary>Gets the counter for terminal session coordination outcomes.</summary>
    public const string SessionOperationCount = "agentkit.session.operation.count";

    /// <summary>Gets the counter for terminal concrete session-store outcomes.</summary>
    public const string SessionStoreOperationCount = "agentkit.session.store.operation.count";

    /// <summary>Gets the counter for terminal authoritative session-directory outcomes.</summary>
    public const string SessionDirectoryOperationCount = "agentkit.session.directory.operation.count";

    /// <summary>Gets the counter for terminal budget reservation outcomes.</summary>
    public const string BudgetReservationCount = "agentkit.budget.reservation.count";

    /// <summary>Gets the counter for terminal budget settlement outcomes.</summary>
    public const string BudgetSettlementCount = "agentkit.budget.settlement.count";

    /// <summary>Gets the counter for terminal budget start-accounting outcomes.</summary>
    public const string BudgetStartCount = "agentkit.budget.start.count";

    /// <summary>Gets the counter for terminal budget correction outcomes.</summary>
    public const string BudgetCorrectionCount = "agentkit.budget.correction.count";

    /// <summary>Gets the counter name for terminal budget scope-creation outcomes.</summary>
    public const string BudgetScopeCreateCount = "agentkit.budget.scope.create.count";

    /// <summary>Gets the counter name for terminal budget snapshot-read outcomes.</summary>
    public const string BudgetSnapshotCount = "agentkit.budget.snapshot.count";

    /// <summary>Gets the counter for terminal authoritative budget-ledger operations.</summary>
    public const string BudgetLedgerOperationCount = "agentkit.budget.ledger.operation.count";

    /// <summary>Gets the histogram for authoritative budget-ledger operation duration in seconds.</summary>
    public const string BudgetLedgerOperationDuration = "agentkit.budget.ledger.operation.duration";

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

    /// <summary>Gets the counter for terminal human-question publication outcomes.</summary>
    public const string HumanQuestionPublicationCount = "agentkit.human_question.publication.count";

    /// <summary>Gets the histogram for human-question publication duration in seconds.</summary>
    public const string HumanQuestionPublicationDuration = "agentkit.human_question.publication.duration";

    /// <summary>Gets the counter for terminal task-delegation dispatch outcomes.</summary>
    public const string TaskDelegationPublicationCount = "agentkit.task_delegation.publication.count";

    /// <summary>Gets the histogram for task-delegation dispatch duration in seconds.</summary>
    public const string TaskDelegationPublicationDuration = "agentkit.task_delegation.publication.duration";

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

    /// <summary>Gets the stable name for the count of local run-event hub operations by bounded operation and outcome.</summary>
    /// <remarks>The counter records terminal local operations in operations, without run identities or event content as dimensions.</remarks>
    public const string RunEventHubOperationCount = "agentkit.run.event.hub.operation.count";

    /// <summary>Gets the stable name for the duration in seconds of local run-event hub operations.</summary>
    /// <remarks>The histogram uses bounded operation and outcome dimensions; an unavailable or invalid clock measurement is omitted.</remarks>
    public const string RunEventHubOperationDuration = "agentkit.run.event.hub.operation.duration";
}
