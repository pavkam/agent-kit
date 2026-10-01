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
    /// <summary>Identifies the exact retained tool catalog version on traces; never a metric dimension.</summary>
    public const string ToolCatalogVersion = "agentkit.tool.catalog.version";

    /// <summary>Identifies a resolved canonical tool on logs and traces.</summary>
    /// <remarks>This high-cardinality identity is distinct from a provider alias and is never a metric dimension.</remarks>
    public const string ToolId = "agentkit.tool.id";

    /// <summary>Identifies the exact captured tool revision on logs and traces.</summary>
    /// <remarks>The value is publication evidence, never a metric dimension or a request to select the latest tool.</remarks>
    public const string ToolVersion = "agentkit.tool.version";

    /// <summary>Identifies the source of a retained tool publication on logs and traces.</summary>
    /// <remarks>The stable source identity is never a metric dimension; descriptor content is excluded.</remarks>
    public const string ToolSourceId = "agentkit.tool.source.id";

    /// <summary>Identifies the exact retained source publication on logs and traces.</summary>
    /// <remarks>The source-defined version is never a metric dimension or authority to perform an effect.</remarks>
    public const string ToolSourceVersion = "agentkit.tool.source.version";


    /// <summary>Gets the bounded session-entry codec operation attribute.</summary>
    public const string SessionEntryCodecOperation = "agentkit.session.entry.codec.operation";

    /// <summary>Gets the durable session-entry identity attribute for traces and structured logs.</summary>
    public const string SessionEntryId = "agentkit.session.entry.id";

    /// <summary>Gets the durable session-branch identity attribute for traces and structured logs.</summary>
    public const string SessionBranchId = "agentkit.session.branch.id";
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

    /// <summary>Gets the bounded provider-operation attribute for traces and metrics.</summary>
    public const string ProviderOperation = "agentkit.provider.operation";

    /// <summary>Gets the OpenTelemetry GenAI tool-name attribute.</summary>
    public const string ToolName = "gen_ai.tool.name";

    /// <summary>Gets the AgentKit tool-call identity attribute.</summary>
    public const string ToolCallId = "agentkit.tool_call.id";

    /// <summary>Identifies the captured projection-policy key on logs and traces, never as a metric dimension.</summary>
    /// <remarks>The value is reference metadata; policy bounds, extensions, and projected content are not copied into this tag.</remarks>
    public const string ToolResultProjectionPolicyKey = "agentkit.tool.result.projection_policy.key";

    /// <summary>Identifies the exact retained projection-policy revision on logs and traces, never as a metric dimension.</summary>
    /// <remarks>The positive numeric revision preserves the requested historical binding without selecting the current version.</remarks>
    public const string ToolResultProjectionPolicyVersion = "agentkit.tool.result.projection_policy.version";

    /// <summary>Gets the stable human-question identity attribute for traces and structured logs.</summary>
    public const string QuestionId = "agentkit.question.id";

    /// <summary>Gets the stable task-delegation identity attribute for traces and structured logs.</summary>
    public const string DelegationId = "agentkit.delegation.id";

    /// <summary>Gets the AgentKit security-request identity attribute.</summary>
    public const string SecurityRequestId = "agentkit.security_request.id";

    /// <summary>Gets the security-grant identity attribute for traces and structured logs.</summary>
    public const string SecurityGrantId = "agentkit.security_grant.id";

    /// <summary>Gets the enforcement-intent identity attribute for traces and structured logs.</summary>
    public const string SecurityEnforcementIntentId = "agentkit.security_enforcement_intent.id";

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

    /// <summary>Gets the budget reservation identity attribute for traces and structured logs.</summary>
    public const string BudgetReservationId = "agentkit.budget.reservation.id";

    /// <summary>Gets the bounded authoritative budget-ledger operation attribute.</summary>
    public const string BudgetOperation = "agentkit.budget.operation";

    /// <summary>Gets the stable hook-point identity attribute.</summary>
    public const string HookPoint = "agentkit.hook.point";

    /// <summary>Gets the stable hook-invocation identity attribute: one registration's individual execution within a dispatch.</summary>
    public const string HookInvocationId = "agentkit.hook.invocation.id";

    /// <summary>Gets the stable hook-dispatch identity attribute: one emission of one hook point, shared by every hook invoked for it.</summary>
    public const string HookDispatchId = "agentkit.hook.dispatch.id";

    /// <summary>Gets the stable hook-registration identity attribute: one configured hook registration within a profile.</summary>
    public const string HookRegistrationId = "agentkit.hook.registration.id";

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

    /// <summary>Gets the configured MCP server key attribute.</summary>
    /// <remarks>The key comes from host configuration and is bounded; it is a span and log attribute, never a metric dimension.</remarks>
    public const string McpServerKey = "agentkit.mcp.server.key";

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

    /// <summary>Gets the stable name for the bounded local run-event hub operation dimension.</summary>
    /// <remarks>Values identify package-defined lifecycle stages, never user labels, run identities, sequence numbers, or event content.</remarks>
    public const string RunEventHubOperation = "agentkit.run.event.hub.operation";

    /// <summary>Gets the worker identity correlation attribute for durable-lease traces and logs.</summary>
    public const string WorkerId = "agentkit.worker.id";

    /// <summary>Gets the stable name for the bounded durable-journal write operation dimension.</summary>
    /// <remarks>Values identify package-defined write stages (start, checkpoint, terminal), never operation content.</remarks>
    public const string DurableJournalOperation = "agentkit.durable.journal.operation";

    /// <summary>Gets the stable name for the bounded durable-coordinator stage dimension.</summary>
    /// <remarks>Values identify package-defined coordinator stages, never operation names, payloads, or identities.</remarks>
    public const string DurableOperationStage = "agentkit.durable.operation.stage";

    /// <summary>Gets the durable artifact identity attribute for traces and structured logs.</summary>
    public const string ArtifactId = "agentkit.artifact.id";

    /// <summary>Gets the staged artifact preparation identity attribute for traces and structured logs.</summary>
    public const string ArtifactPreparationId = "agentkit.artifact.preparation.id";

    /// <summary>Gets the bounded artifact operation attribute.</summary>
    public const string ArtifactOperation = "agentkit.artifact.operation";

    /// <summary>Gets the artifact coordinator key attribute for traces and structured logs.</summary>
    public const string ArtifactCoordinatorKey = "agentkit.artifact.coordinator.key";

    /// <summary>Gets the bounded artifact store adapter attribute, such as <c>in_memory</c>, <c>sqlite</c>, <c>json</c>, or <c>file_system</c>.</summary>
    public const string ArtifactStoreAdapter = "agentkit.artifact.store.adapter";

    /// <summary>Gets the bounded artifact store operation attribute.</summary>
    public const string ArtifactStoreOperation = "agentkit.artifact.store.operation";

    /// <summary>Gets the tag naming the goal a goal or delegation operation concerns.</summary>
    public const string GoalId = "agentkit.goal.id";

    /// <summary>Gets the tag naming the goal attempt an operation concerns.</summary>
    public const string GoalAttemptId = "agentkit.goal.attempt.id";

    /// <summary>Gets the tag naming a goal's resulting status.</summary>
    /// <remarks>The value comes from the bounded status vocabulary.</remarks>
    public const string GoalStatus = "agentkit.goal.status";

    /// <summary>Gets the tag naming the bounded goal-store operation.</summary>
    public const string GoalStoreOperation = "agentkit.goal.store.operation";

    /// <summary>Gets the tag naming the join strategy key a join evaluated.</summary>
    public const string GoalJoinStrategy = "agentkit.goal.join.strategy";

    /// <summary>Gets the tag naming the bounded goal-store adapter that served an operation.</summary>
    public const string GoalStoreAdapter = "agentkit.goal.store.adapter";

    /// <summary>Gets the tag naming a goal's prior status on a transition.</summary>
    /// <remarks>The value comes from the bounded status vocabulary.</remarks>
    public const string GoalFromStatus = "agentkit.goal.from_status";

    /// <summary>Gets the tag naming the bounded memory-store adapter that served an operation.</summary>
    public const string MemoryStoreAdapter = "agentkit.memory.store.adapter";

    /// <summary>Gets the tag naming the bounded state family (memory, document, or vector) a store operation concerns.</summary>
    public const string MemoryStoreFamily = "agentkit.memory.store.family";

    /// <summary>Gets the tag naming the bounded memory-store operation.</summary>
    public const string MemoryStoreOperation = "agentkit.memory.store.operation";

    /// <summary>Gets the tag naming the durable memory an operation concerns.</summary>
    public const string MemoryId = "agentkit.memory.id";

    /// <summary>Gets the tag naming the document an operation concerns.</summary>
    public const string MemoryDocumentId = "agentkit.memory.document.id";

    /// <summary>Gets the tag naming the memory profile key an operation ran under.</summary>
    public const string MemoryProfileKey = "agentkit.memory.profile.key";

    /// <summary>Gets the tag naming the memory profile version an operation ran under.</summary>
    public const string MemoryProfileVersion = "agentkit.memory.profile.version";

    /// <summary>Gets the tag naming the retrieval request an operation concerns.</summary>
    public const string RetrievalRequestId = "agentkit.retrieval.request.id";

    /// <summary>Gets the tag naming the retrieval source key a source search concerns.</summary>
    public const string RetrievalSourceKey = "agentkit.retrieval.source.key";

    /// <summary>Gets the tag naming the bounded reason candidates were omitted from a retrieval.</summary>
    public const string RetrievalOmissionReason = "agentkit.retrieval.omission.reason";

    /// <summary>Gets the tag naming the number of candidates a retrieval exposed.</summary>
    public const string RetrievalCandidateCount = "agentkit.retrieval.candidate.count";

    /// <summary>Gets the tag naming the exporter key an observation sink delivers under.</summary>
    /// <remarks>The key comes from host configuration and is bounded.</remarks>
    public const string ObservationExporterKey = "agentkit.observation.exporter.key";

    /// <summary>Gets the tag naming the exporter options generation an observation was delivered under.</summary>
    public const string ObservationExporterVersion = "agentkit.observation.exporter.version";

    /// <summary>Gets the tag naming the bounded run-event kind an observation concerns.</summary>
    public const string ObservationEventKind = "agentkit.observation.event.kind";

    /// <summary>Gets the tag carrying the run event's per-run sequence.</summary>
    /// <remarks>The sequence is correlation evidence on spans and logs only, never a metric dimension.</remarks>
    public const string ObservationEventSequence = "agentkit.observation.event.sequence";

    /// <summary>Gets the tag naming the kind of captured observation content.</summary>
    public const string ObservationContentKind = "agentkit.observation.content.kind";

    /// <summary>Gets the tag naming the declared classification of captured observation content.</summary>
    public const string ObservationContentClassification = "agentkit.observation.content.classification";

    /// <summary>Gets the tag carrying the fingerprint of captured observation content.</summary>
    public const string ObservationContentFingerprint = "agentkit.observation.content.fingerprint";

    /// <summary>Gets the tag carrying redacted observation content that survived policy.</summary>
    /// <remarks>Present only when content capture is explicitly enabled and a redactor returned the content.</remarks>
    public const string ObservationContentValue = "agentkit.observation.content.value";

    /// <summary>Gets the tag naming the bounded reason captured content was omitted.</summary>
    public const string ObservationContentOmittedReason = "agentkit.observation.content.omitted.reason";

    /// <summary>Gets the tag naming the evaluation run an operation concerns.</summary>
    /// <remarks>The run identity is correlation evidence on spans and logs only, never a metric dimension.</remarks>
    public const string EvaluationRunId = "agentkit.evaluation.run.id";

    /// <summary>Gets the tag naming the evaluation plan an operation concerns.</summary>
    public const string EvaluationPlanId = "agentkit.evaluation.plan.id";

    /// <summary>Gets the tag naming the evaluation plan version an operation concerns.</summary>
    public const string EvaluationPlanVersion = "agentkit.evaluation.plan.version";

    /// <summary>Gets the tag naming the evaluation case an operation concerns.</summary>
    public const string EvaluationCaseId = "agentkit.evaluation.case.id";

    /// <summary>Gets the tag carrying the one-based repetition of an evaluation case.</summary>
    /// <remarks>The repetition is correlation evidence on spans and logs only, never a metric dimension.</remarks>
    public const string EvaluationCaseRepetition = "agentkit.evaluation.case.repetition";

    /// <summary>Gets the tag naming the bounded disposition of an evaluation case repetition.</summary>
    public const string EvaluationCaseDisposition = "agentkit.evaluation.case.disposition";

    /// <summary>Gets the tag naming the configured evaluator key an evaluator invocation ran under.</summary>
    /// <remarks>The key comes from host configuration and is bounded.</remarks>
    public const string EvaluationEvaluatorKey = "agentkit.evaluation.evaluator.key";

    /// <summary>Gets the tag naming the configured report exporter key a delivery used.</summary>
    /// <remarks>The key comes from host configuration and is bounded.</remarks>
    public const string EvaluationExporterKey = "agentkit.evaluation.exporter.key";

    /// <summary>Gets the tag naming the configured result-store key a plan selected.</summary>
    /// <remarks>The key comes from host configuration and is bounded.</remarks>
    public const string EvaluationResultStoreKey = "agentkit.evaluation.result_store.key";

    /// <summary>Gets the tag naming the bounded evaluation result-store adapter that served an operation.</summary>
    public const string EvaluationStoreAdapter = "agentkit.evaluation.store.adapter";

    /// <summary>Gets the tag naming the bounded evaluation result-store operation.</summary>
    public const string EvaluationStoreOperation = "agentkit.evaluation.store.operation";
}
