// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Defines stable names for activities emitted by AgentKit components.</summary>
/// <remarks>Names describe operations, never high-cardinality identities or content.</remarks>
public static class AgentKitActivityNames
{
    /// <summary>Identifies complete bounded canonical tool-schema compilation before model exposure.</summary>
    public const string ToolSchemaCompile = "tool.schema.compile";
    /// <summary>Identifies bounded instance validation against a retained canonical tool schema.</summary>
    public const string ToolSchemaValidate = "tool.schema.validate";
    /// <summary>Complete selected-source discovery and partial-owner cleanup.</summary>
    public const string ToolCatalogDiscover = "tool.catalog.discover";
    /// <summary>One exact provider discovery and publication validation.</summary>
    public const string ToolCatalogDiscoverSource = "tool.catalog.discover.source";
    /// <summary>Closure of a discovery owner before or after catalog handoff.</summary>
    public const string ToolCatalogDiscoveryClose = "tool.catalog.discovery.close";
    /// <summary>Complete or partial discovered-source cleanup.</summary>
    public const string ToolCatalogDiscoveryDisposeSources = "tool.catalog.discovery.dispose_sources";
    /// <summary>One exact discovered-source cleanup and its completion or failure.</summary>
    public const string ToolCatalogDiscoveryDisposeSource = "tool.catalog.discovery.dispose_source";
    /// <summary>Atomic handoff of retained discovery ownership to a validated catalog.</summary>
    public const string ToolCatalogDiscoveryTransfer = "tool.catalog.discovery.transfer";
    /// <summary>Identifies complete local toolset and source-provider selection before discovery.</summary>
    public const string ToolRegistrationSelect = "tool.registration.select";
    /// <summary>Identifies validation and policy coordination for one captured tool catalog.</summary>
    public const string ToolCatalogMerge = "tool.catalog.merge";
    /// <summary>Identifies one complete catalog collision-policy decision.</summary>
    public const string ToolCatalogMergePolicy = "tool.catalog.merge.policy";
    /// <summary>Names source discovery that transfers a retained tool-provider capture, without invoking a tool.</summary>
    public const string ToolProviderDiscover = "tool.provider.discover";

    /// <summary>Names exact invoker acquisition through a retained catalog graph.</summary>
    public const string ToolCatalogInvokerAcquire = "tool.catalog.invoker.acquire";
    /// <summary>Names release of a catalog-owned source invoker lease.</summary>
    public const string ToolCatalogInvokerRelease = "tool.catalog.invoker.release";
    /// <summary>Names catalog closure while pending acquisitions and leases drain.</summary>
    public const string ToolCatalogCaptureClose = "tool.catalog.capture.close";
    /// <summary>Names disposal of every retained source after catalog work drains.</summary>
    public const string ToolCatalogCaptureDisposeSources = "tool.catalog.capture.dispose_sources";

    /// <summary>Identifies exact invoker acquisition from a retained source capture.</summary>
    /// <remarks>Successful acquisition retains a lifetime only; it never describes a tool invocation.</remarks>
    public const string ToolInvokerAcquire = "tool.invoker.acquire";

    /// <summary>Identifies capture closure, including the wait for outstanding invoker leases.</summary>
    /// <remarks>Completion means owned resources have settled, not that external tool effects were undone.</remarks>
    public const string ToolProviderCaptureClose = "tool.provider.capture.close";

    /// <summary>Identifies release of one invoker acquisition.</summary>
    /// <remarks>The final release may await cleanup of its closed source capture.</remarks>
    public const string ToolInvokerRelease = "tool.invoker.release";

    /// <summary>Identifies the single cleanup of a source capture's owned resource lifetime.</summary>
    /// <remarks>Borrowed invokers are never disposed directly by this operation.</remarks>
    public const string ToolProviderCaptureDisposeResources = "tool.provider.capture.dispose_resources";


    /// <summary>Gets the name for one bounded session-entry codec operation.</summary>
    public const string SessionEntryCodec = "session.entry.codec";
    /// <summary>Gets the name for validating and building one AgentKit service provider.</summary>
    public const string AgentCompositionBuild = "agent.composition.build";

    /// <summary>Gets the name for validating a pinned definition before a new agent run is admitted.</summary>
    public const string AgentAdmission = "agent.admission";

    /// <summary>Gets the name for resolving a trusted identity assertion.</summary>
    public const string IdentityResolve = "identity.resolve";

    /// <summary>Gets the name for deriving a narrower delegated identity.</summary>
    public const string IdentityDerive = "identity.derive";

    /// <summary>Gets the OpenTelemetry GenAI-compatible agent invocation name.</summary>
    public const string InvokeAgent = "invoke_agent";

    /// <summary>Gets the name for coordinating one logical agent turn.</summary>
    public const string AgentTurn = "agent.turn";

    /// <summary>Gets the name for evaluating one immutable run-continuation snapshot.</summary>
    public const string RunContinuationEvaluate = "run.continuation.evaluate";

    /// <summary>Gets the name for deterministic planning of already admitted input promotion.</summary>
    public const string InputPromotionPlan = "input.promotion.plan";

    /// <summary>Gets the name for coordinating one durable input admission.</summary>
    public const string InputAdmission = "input.admission";

    /// <summary>Gets the name for coordinating one atomic input promotion.</summary>
    public const string InputPromotion = "input.promotion";

    /// <summary>Gets the name for preparing provider-ready working context.</summary>
    public const string ContextPrepare = "context.prepare";

    /// <summary>Gets the OpenTelemetry GenAI-compatible model inference name.</summary>
    public const string Chat = "chat";

    /// <summary>Gets the name for consuming a streamed provider response.</summary>
    public const string ModelStream = "model.stream";

    /// <summary>Gets the name for coordinating a model-requested tool batch.</summary>
    public const string ToolBatch = "tool.batch";

    /// <summary>Gets the OpenTelemetry GenAI-compatible tool execution name.</summary>
    public const string ExecuteTool = "execute_tool";

    /// <summary>Resolves the exact retained policy revision for one tool-result projection.</summary>
    /// <remarks>The activity covers one lookup and its terminal outcome, without performing projection or tool invocation.</remarks>
    public const string ToolResultProjectionPolicyResolve = "tool.result.projection_policy.resolve";

    /// <summary>Gets the name for validating a terminal structured output.</summary>
    public const string OutputValidate = "output.validate";

    /// <summary>Gets the name for bounded local output-schema preflight.</summary>
    public const string OutputSchemaPreflight = "output.schema.preflight";

    /// <summary>Gets the name for bounded local output-schema candidate evaluation.</summary>
    public const string OutputSchemaEvaluate = "output.schema.evaluate";

    /// <summary>Gets the name for committing session state.</summary>
    public const string SessionCommit = "session.commit";

    /// <summary>Gets the name for creating a session record.</summary>
    public const string SessionCreate = "session.create";

    /// <summary>Gets the name for loading a session descriptor.</summary>
    public const string SessionLoad = "session.load";

    /// <summary>Gets the name for reading a bounded session page.</summary>
    public const string SessionRead = "session.read";

    /// <summary>Gets the name for branching immutable session history.</summary>
    public const string SessionBranch = "session.branch";

    /// <summary>Gets the name for deleting a session record.</summary>
    public const string SessionDelete = "session.delete";

    /// <summary>Gets the name for one concrete session-store operation.</summary>
    public const string SessionStoreOperation = "session.store.operation";

    /// <summary>Gets the name for one authoritative session-directory operation.</summary>
    public const string SessionDirectoryOperation = "session.directory.operation";

    /// <summary>Gets the name for one coordinated, authorized session-directory listing.</summary>
    public const string SessionDirectoryList = "session.directory.list";

    /// <summary>Gets the name for acquiring process-local run ownership.</summary>
    public const string SessionLeaseAcquire = "session.lease.acquire";

    /// <summary>Names protected admitted-input lookup.</summary>
    public const string SessionInputLookup = "session.input.lookup";

    /// <summary>Names protected execution-lane provisioning.</summary>
    public const string SessionLaneProvision = "session.lane.provision";

    /// <summary>Names protected input admission.</summary>
    public const string SessionInputAdmit = "session.input.admit";

    /// <summary>Names atomic accepted-run installation.</summary>
    public const string SessionRunAccept = "session.run.accept";

    /// <summary>Names atomic release of a lane's installed accepted-run state.</summary>
    public const string SessionRunRelease = "session.run.release";

    /// <summary>Names protected accepted-run-state loading.</summary>
    public const string SessionRunStateLoad = "session.run.state.load";

    /// <summary>Gets the name for publishing a session event to one sink.</summary>
    public const string SessionEventPublish = "session.event.publish";

    /// <summary>Gets the name for completing all run-owned settlement work.</summary>
    public const string RunSettle = "run.settle";

    /// <summary>Gets the name for capturing one exact security profile and authorization scope.</summary>
    public const string SecurityProfileCapture = "security.profile.capture";

    /// <summary>Gets the name for a security authorization decision.</summary>
    public const string SecurityAuthorize = "security.authorize";

    /// <summary>Gets the name for activating one captured security-authority binding.</summary>
    public const string SecurityAuthoritySelect = "security.authority.select";

    /// <summary>Gets the name for delivering one immutable security audit record to its captured sinks.</summary>
    public const string SecurityAuditDispatch = "security.audit.dispatch";

    /// <summary>Gets the name for atomic grant consumption and enforcement-intent receipt persistence.</summary>
    public const string SecurityGrantConsume = "security.grant.consume";

    /// <summary>Gets the name for one authoritative security-grant store operation.</summary>
    public const string SecurityGrantStoreOperation = "security.grant.store.operation";

    /// <summary>Gets the name for reserving bounded shared capacity.</summary>
    public const string BudgetReserve = "budget.reserve";

    /// <summary>Gets the name for creating a child budget scope.</summary>
    public const string BudgetScopeCreate = "budget.scope.create";

    /// <summary>Gets the name for settling reserved capacity.</summary>
    public const string BudgetCommit = "budget.commit";

    /// <summary>Gets the name for moving reserved capacity into started accounting.</summary>
    public const string BudgetStart = "budget.start";

    /// <summary>Gets the name for replacing provisional budget accounting with a correction.</summary>
    public const string BudgetCorrection = "budget.correction";

    /// <summary>Gets the name for reading one point-in-time budget snapshot.</summary>
    public const string BudgetSnapshot = "budget.snapshot";

    /// <summary>Gets the name for releasing or retaining one owned reservation during disposal.</summary>
    public const string BudgetRelease = "budget.release";

    /// <summary>Gets the name for one authoritative budget-ledger operation.</summary>
    public const string BudgetLedgerOperation = "budget.ledger.operation";

    /// <summary>Gets the name for dispatching one typed hook point.</summary>
    public const string HookDispatch = "hook.dispatch";

    /// <summary>Gets the name for compacting eligible context history.</summary>
    public const string ContextCompact = "context.compact";

    /// <summary>Gets the name for one protected human-question publication attempt.</summary>
    public const string HumanQuestionPublish = "human.question.publish";

    /// <summary>Gets the name for one protected task-delegation dispatch attempt.</summary>
    public const string TaskDelegationDispatch = "task.delegation.dispatch";

    /// <summary>Gets the name for a protected file-system host operation.</summary>
    public const string FileSystemOperation = "filesystem.operation";

    /// <summary>Gets the name for canonical process-intent resolution.</summary>
    public const string ProcessResolve = "process.resolve";

    /// <summary>Gets the name for preparing an enforceable process sandbox.</summary>
    public const string ProcessSandboxPrepare = "process.sandbox.prepare";

    /// <summary>Gets the name for one bounded process execution.</summary>
    public const string ProcessRun = "process.run";

    /// <summary>Gets the name for connecting and negotiating one MCP client session.</summary>
    public const string McpConnect = "mcp.connect";

    /// <summary>Gets the name for validating and publishing one MCP tool-catalog generation.</summary>
    public const string McpCatalogRefresh = "mcp.catalog.refresh";

    /// <summary>Gets the name for one remote MCP tool invocation.</summary>
    public const string McpToolCall = "mcp.tool.call";

    /// <summary>Gets the name for composing and publishing one model-catalog generation.</summary>
    public const string ModelCatalogRefresh = "model.catalog.refresh";

    /// <summary>Gets the name for selecting one compatible model registration.</summary>
    public const string ModelSelect = "model.select";

    /// <summary>Gets the name for one bounded language-intelligence query.</summary>
    public const string LanguageQuery = "language.query";

    /// <summary>Gets the name for one bounded network destination resolution.</summary>
    public const string NetworkResolve = "network.resolve";

    /// <summary>Gets the name for one bounded network request dispatch.</summary>
    public const string NetworkSend = "network.send";

    /// <summary>Gets the stable name for one run-local event publication, subscription, or delivery operation.</summary>
    /// <remarks>The operation tag distinguishes bounded fan-out stages; this activity does not attest durable publication or run settlement.</remarks>
    public const string RunEventHub = "run.event.hub";

    /// <summary>Gets the name for one durable-operation execution-lease acquisition attempt.</summary>
    public const string DurableLeaseAcquire = "durable.lease.acquire";

    /// <summary>Gets the name for one durable-operation execution-lease renewal attempt.</summary>
    public const string DurableLeaseRenew = "durable.lease.renew";

    /// <summary>Gets the name for one durable-operation execution-lease release.</summary>
    public const string DurableLeaseRelease = "durable.lease.release";

    /// <summary>Gets the name for one durable-journal write, distinguished by its bounded operation tag.</summary>
    public const string DurableJournalWrite = "durable.journal.write";

    /// <summary>Gets the name for one durable-journal evidence load.</summary>
    public const string DurableJournalLoadEvidence = "durable.journal.load_evidence";

    /// <summary>Gets the name for submitting one conversational turn through a conversation session.</summary>
    /// <remarks>The activity covers session creation, message admission, and the driven agent-loop run as one causal unit.</remarks>
    public const string ConversationTurn = "conversation.turn";
}
