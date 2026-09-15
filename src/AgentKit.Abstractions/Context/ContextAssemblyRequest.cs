// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to assemble a bounded, provider-ready
/// <see cref="LlmRequestContext"/> for a single model request.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>ContextAssemblyRequest</c> described by the context architecture,
/// which additionally carries an <c>AgentDefinition</c>, a durable history
/// cursor resolved through session contracts, a full
/// <c>SecurityAuthorizationContext</c>, a hook dispatch context, an
/// effective-configuration snapshot, and a context budget. Until those
/// packages exist, the caller (the agent loop) supplies the already-loaded
/// eligible history directly and the assembler performs only structural
/// repair and instruction/tool assembly over exactly the fields declared
/// here.
/// </para>
/// </remarks>
public sealed record ContextAssemblyRequest
{
    /// <summary>Initializes a new instance of the <see cref="ContextAssemblyRequest"/> record.</summary>
    /// <param name="agentId">The agent this request is being assembled for.</param>
    /// <param name="sessionId">The session this request's history was loaded from.</param>
    /// <param name="branchId">The branch this request's history was loaded from.</param>
    /// <param name="runId">The run this request is being assembled within.</param>
    /// <param name="turnId">The turn this request is being assembled for.</param>
    /// <param name="modelRequestId">
    /// The identity allocated by the loop for this model request, flowing
    /// through the resulting <see cref="LlmRequestContext"/> and every
    /// subsequent event and committed message it produces.
    /// </param>
    /// <param name="model">The selected model descriptor.</param>
    /// <param name="instructions">
    /// The system and developer instructions to place first in the
    /// assembled request, in order.
    /// </param>
    /// <param name="history">The eligible conversation history, in ascending commit order.</param>
    /// <param name="tools">The tools available for the model to call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/>, <paramref name="toolChoice"/>,
    /// <paramref name="settings"/>, or <paramref name="extensions"/> is
    /// null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="instructions"/>, <paramref name="history"/>, or
    /// <paramref name="tools"/> is a default, uninitialized array.
    /// </exception>
    public ContextAssemblyRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        TurnId turnId,
        ModelRequestId modelRequestId,
        ModelDescriptor model,
        ImmutableArray<AgentMessage> instructions,
        ImmutableArray<AgentMessage> history,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfDefault(instructions);
        ArgumentException.ThrowIfDefault(history);
        ArgumentException.ThrowIfDefault(tools);
        ArgumentNullException.ThrowIfNull(toolChoice);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(extensions);

        AgentId = agentId;
        SessionId = sessionId;
        BranchId = branchId;
        RunId = runId;
        TurnId = turnId;
        ModelRequestId = modelRequestId;
        Model = model;
        Instructions = instructions;
        History = history;
        Tools = tools;
        ToolChoice = toolChoice;
        Settings = settings;
        Extensions = extensions;
    }

    /// <summary>Initializes a request from one atomically captured context-evidence value.</summary>
    /// <param name="agentId">The agent this request is being assembled for.</param>
    /// <param name="sessionId">The session this request's history was loaded from.</param>
    /// <param name="branchId">The branch this request's history was loaded from.</param>
    /// <param name="runId">The active run correlated by the authorization evidence.</param>
    /// <param name="turnId">The active turn correlated by the authorization evidence.</param>
    /// <param name="modelRequestId">The identity allocated for this model request.</param>
    /// <param name="model">The selected model descriptor.</param>
    /// <param name="instructions">The ordered system and developer instructions.</param>
    /// <param name="evidence">The atomic definition, identity, history, authorization, and configuration evidence.</param>
    /// <param name="tools">The tools available for the model to call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/>, <paramref name="model"/>, <paramref name="toolChoice"/>, <paramref name="settings"/>, or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentException">Outer agent, session, branch, run, or turn coordinates differ from the captured evidence, or an array is uninitialized.</exception>
    public ContextAssemblyRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        TurnId turnId,
        ModelRequestId modelRequestId,
        ModelDescriptor model,
        ImmutableArray<AgentMessage> instructions,
        ContextAssemblyEvidence evidence,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        ExtensionData extensions)
        : this(
            agentId,
            sessionId,
            branchId,
            runId,
            turnId,
            modelRequestId,
            model,
            instructions,
            ValidateEvidence(agentId, sessionId, branchId, runId, turnId, evidence),
            tools,
            toolChoice,
            settings,
            extensions)
        => Evidence = evidence;

    /// <summary>Gets the agent this request is being assembled for.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this request's history was loaded from.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the branch this request's history was loaded from.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the run this request is being assembled within.</summary>
    public RunId RunId { get; init; }

    /// <summary>Gets the turn this request is being assembled for.</summary>
    public TurnId TurnId { get; init; }

    /// <summary>
    /// Gets the identity allocated by the loop for this model request.
    /// </summary>
    public ModelRequestId ModelRequestId { get; init; }

    /// <summary>Gets the selected model descriptor.</summary>
    public ModelDescriptor Model { get; init; }

    /// <summary>Gets the system and developer instructions to place first in the assembled request.</summary>
    public ImmutableArray<AgentMessage> Instructions { get; init; }

    /// <summary>Gets the eligible conversation history, in ascending commit order.</summary>
    public ImmutableArray<AgentMessage> History { get; init; }

    /// <summary>Gets the atomic assembly evidence when the evidence-aware constructor was used.</summary>
    /// <value>The captured evidence, or null for requests created through the compatibility constructor.</value>
    public ContextAssemblyEvidence? Evidence { get; }

    /// <summary>Gets the tools available for the model to call.</summary>
    public ImmutableArray<LlmToolDefinition> Tools { get; init; }

    /// <summary>Gets the tool-call selection policy.</summary>
    public LlmToolChoice ToolChoice { get; init; }

    /// <summary>Gets the effective sampling and output settings.</summary>
    public LlmRequestSettings Settings { get; init; }

    /// <summary>Gets caller-specific or forward-compatible request data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(ContextAssemblyRequest? other) =>
        other is not null
        && AgentId.Equals(other.AgentId)
        && SessionId.Equals(other.SessionId)
        && BranchId.Equals(other.BranchId)
        && RunId.Equals(other.RunId)
        && TurnId.Equals(other.TurnId)
        && ModelRequestId.Equals(other.ModelRequestId)
        && Model.Equals(other.Model)
        && Instructions.SequenceEqual(other.Instructions)
        && History.SequenceEqual(other.History)
        && Equals(Evidence, other.Evidence)
        && Tools.SequenceEqual(other.Tools)
        && ToolChoice.Equals(other.ToolChoice)
        && Settings.Equals(other.Settings)
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(BranchId);
        hash.Add(RunId);
        hash.Add(TurnId);
        hash.Add(ModelRequestId);
        hash.Add(Model);
        foreach (var message in Instructions)
        {
            hash.Add(message);
        }

        foreach (var message in History)
        {
            hash.Add(message);
        }

        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(ToolChoice);
        hash.Add(Evidence);
        hash.Add(Settings);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }

    private static ImmutableArray<AgentMessage> ValidateEvidence(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        TurnId turnId,
        ContextAssemblyEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNotEqual(agentId, evidence.Agent.Id, nameof(agentId));
        ArgumentException.ThrowIfNotEqual(sessionId, evidence.History.SourceCursor.SessionId, nameof(sessionId));
        ArgumentException.ThrowIfNotEqual(branchId, evidence.History.SourceCursor.BranchId, nameof(branchId));
        ArgumentException.ThrowIfNotEqual(evidence.Authorization.Scope.Correlation is InRunOperationCorrelation, true, nameof(evidence));

        var correlation = (InRunOperationCorrelation) evidence.Authorization.Scope.Correlation;
        ArgumentException.ThrowIfNotEqual(runId, correlation.RunId, nameof(runId));
        ArgumentException.ThrowIfNotEqual(turnId, correlation.TurnId, nameof(turnId));
        return evidence.History.Messages;
    }
}
