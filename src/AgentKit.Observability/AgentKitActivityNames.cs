// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Defines stable names for activities emitted by AgentKit components.</summary>
/// <remarks>Names describe operations, never high-cardinality identities or content.</remarks>
public static class AgentKitActivityNames
{
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

    /// <summary>Gets the name for acquiring process-local run ownership.</summary>
    public const string SessionLeaseAcquire = "session.lease.acquire";

    /// <summary>Gets the name for publishing a session event to one sink.</summary>
    public const string SessionEventPublish = "session.event.publish";

    /// <summary>Gets the name for completing all run-owned settlement work.</summary>
    public const string RunSettle = "run.settle";

    /// <summary>Gets the name for a security authorization decision.</summary>
    public const string SecurityAuthorize = "security.authorize";

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

    /// <summary>Gets the name for dispatching one typed hook point.</summary>
    public const string HookDispatch = "hook.dispatch";

    /// <summary>Gets the name for compacting eligible context history.</summary>
    public const string ContextCompact = "context.compact";

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
}
