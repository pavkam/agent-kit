// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable bundle of per-agent collaborators an <see cref="IAgentLoop"/> needs to drive one run, compiled
/// once by the run-activation boundary and supplied to <see cref="IAgentLoop.RunAsync"/> as an explicit
/// parameter.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the fuller <c>AgentRunServices</c> described by the agent-runtime
/// architecture, which additionally carries an <c>IInputCoordinator</c>, a <c>SessionExecutionCapability</c>, an
/// <c>IModelRequestExecutor</c>, an <c>IToolExecutor</c>, an <c>IOutputProcessor</c>, an
/// <c>IOutputPublisher</c>, an <c>IHookDispatcher</c>, and a <c>BudgetExecutionCapability</c>. Until those
/// packages exist and are wired into the reduced loop, this bundle carries exactly the collaborators
/// <c>DefaultAgentLoop</c> uses today: session coordination, fresh per-operation authorization capture, context
/// assembly, tool invocation, model catalog and selection, model resolution, and the run continuation policy.
/// </para>
/// <para>
/// The run-activation boundary (the facade's <c>AgentEngine</c>) compiles one instance of this bundle per run,
/// inside the freshly created run scope, by resolving each collaborator through the dependency-injection
/// container — preferring a registration keyed to the same <see cref="ComponentKey{TContract}"/> selected for
/// the run's <see cref="IAgentLoop"/> and falling back to the engine-wide unkeyed registration when no such
/// keyed variant exists. This is what lets a host register a distinct collaborator for one keyed loop selection
/// without disturbing every other agent definition that shares the engine-wide default.
/// </para>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is safe to share across threads
/// without synchronization; every referenced collaborator is expected to be thread-safe for concurrent use by
/// different run scopes, exactly as it was when resolved through ordinary constructor injection.
/// </para>
/// </remarks>
public sealed class AgentRunServices
{
    /// <summary>Initializes one compiled bundle of per-run collaborators.</summary>
    /// <param name="session">Loads eligible history and commits every message and terminal tool result this run produces.</param>
    /// <param name="securityProfileSelector">Captures fresh authorization for each newly identified run or turn operation.</param>
    /// <param name="context">Assembles the provider-ready request for each turn.</param>
    /// <param name="tools">Resolves, authorizes, and invokes every requested tool call.</param>
    /// <param name="models">Supplies the engine-wide versioned view of configured models.</param>
    /// <param name="modelSelector">Chooses one configured model for this run.</param>
    /// <param name="modelResolver">Resolves the chosen model descriptor to its executable provider adapter.</param>
    /// <param name="continuationPolicy">Decides, at every committed-turn boundary, whether the run continues, completes, or halts.</param>
    /// <param name="output">
    /// Validates the terminal assistant response against the run's selected <see cref="OutputDefinition"/>, or
    /// <see langword="null"/> when the composition selects no output processor. A run whose request names an
    /// output definition fails closed when this is <see langword="null"/>.
    /// </param>
    /// <param name="compactor">
    /// Produces and activates a compaction checkpoint over older history when the loop detects context pressure,
    /// or <see langword="null"/> when the composition selects no compactor; the loop then never compacts.
    /// </param>
    /// <param name="budgets">
    /// Creates the run's budget scope and serves its reservations when the request declares limits, or
    /// <see langword="null"/> when the composition selects no budget authority; a budgeted request then fails closed.
    /// </param>
    /// <exception cref="ArgumentNullException">Any required parameter is <see langword="null"/>.</exception>
    public AgentRunServices(
        ISessionCoordinator session,
        ISecurityProfileSelector securityProfileSelector,
        IContextAssembler context,
        IToolInvoker tools,
        IModelCatalog models,
        IModelSelector modelSelector,
        ILlmModelResolver modelResolver,
        IRunContinuationPolicy continuationPolicy,
        IOutputProcessor? output = null,
        ICompactor? compactor = null,
        IBudgetAuthority? budgets = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(securityProfileSelector);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(modelSelector);
        ArgumentNullException.ThrowIfNull(modelResolver);
        ArgumentNullException.ThrowIfNull(continuationPolicy);

        Session = session;
        SecurityProfileSelector = securityProfileSelector;
        Context = context;
        Tools = tools;
        Models = models;
        ModelSelector = modelSelector;
        ModelResolver = modelResolver;
        ContinuationPolicy = continuationPolicy;
        Output = output;
        Compactor = compactor;
        Budgets = budgets;
    }

    /// <summary>Gets the collaborator that loads eligible history and commits every message and terminal tool result.</summary>
    public ISessionCoordinator Session { get; }

    /// <summary>Gets the collaborator that captures fresh authorization for each run or turn operation.</summary>
    public ISecurityProfileSelector SecurityProfileSelector { get; }

    /// <summary>Gets the collaborator that assembles the provider-ready request for each turn.</summary>
    public IContextAssembler Context { get; }

    /// <summary>Gets the collaborator that resolves, authorizes, and invokes every requested tool call.</summary>
    public IToolInvoker Tools { get; }

    /// <summary>Gets the engine-wide versioned view of configured models.</summary>
    public IModelCatalog Models { get; }

    /// <summary>Gets the collaborator that chooses one configured model for this run.</summary>
    public IModelSelector ModelSelector { get; }

    /// <summary>Gets the collaborator that resolves a chosen model descriptor to its executable provider adapter.</summary>
    public ILlmModelResolver ModelResolver { get; }

    /// <summary>Gets the policy consulted at every committed-turn boundary to decide continuation.</summary>
    public IRunContinuationPolicy ContinuationPolicy { get; }

    /// <summary>Gets the processor that validates terminal responses against a selected output definition.</summary>
    /// <value><see langword="null"/> when the composition selects no output processor; runs with an output definition then fail closed.</value>
    public IOutputProcessor? Output { get; }

    /// <summary>Gets the compactor the loop asks to checkpoint older history under context pressure.</summary>
    /// <value><see langword="null"/> when the composition selects no compactor; the loop then never compacts.</value>
    public ICompactor? Compactor { get; }

    /// <summary>Gets the budget authority the loop reserves a budgeted run's capacity through.</summary>
    /// <value><see langword="null"/> when the composition selects none; a request with budget limits then fails closed.</value>
    public IBudgetAuthority? Budgets { get; }
}
