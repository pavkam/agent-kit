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
/// Every request carries one atomically captured <see cref="ContextAssemblyEvidence"/>:
/// the exact admitted <see cref="AgentDefinition"/>, the authenticated
/// <see cref="ExecutionIdentity"/>, the pinned <see cref="HistoryView"/>, the
/// turn's <see cref="SecurityAuthorizationContext"/>, and the
/// <see cref="EffectiveConfigurationSnapshot"/>. The outer coordinates
/// (agent, session, branch, run, and turn) are cross-checked against that
/// evidence at construction, so they can never describe a different run than
/// the evidence does. The history and the instruction sources the assembler
/// resolves are read from the evidence; the request itself adds only the
/// per-model-request values: model, tools, tool choice, settings, extension
/// data, and the optional output contract. There is no evidence-less request.
/// </para>
/// <para>
/// Hook dispatch and the context budget are not request fields: the keyed
/// assembler is composed with its hook dispatcher and budget allocator
/// (<c>ContextAssemblerServices</c>), and the budget is derived from the
/// selected <see cref="ModelDescriptor"/>'s limits and the assembler's options.
/// </para>
/// </remarks>
public sealed record ContextAssemblyRequest
{
    /// <summary>Initializes a request from one atomically captured context-evidence value.</summary>
    /// <param name="agentId">The agent this request is being assembled for.</param>
    /// <param name="sessionId">The session this request's history was loaded from.</param>
    /// <param name="branchId">The branch this request's history was loaded from.</param>
    /// <param name="runId">The active run correlated by the authorization evidence.</param>
    /// <param name="turnId">The active turn correlated by the authorization evidence.</param>
    /// <param name="modelRequestId">
    /// The identity allocated by the loop for this model request, flowing
    /// through the resulting <see cref="LlmRequestContext"/> and every
    /// subsequent event and committed message it produces.
    /// </param>
    /// <param name="model">The selected model descriptor.</param>
    /// <param name="evidence">The atomic definition, identity, history, authorization, and configuration evidence.</param>
    /// <param name="tools">The tools available for the model to call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/>, <paramref name="model"/>, <paramref name="toolChoice"/>, <paramref name="settings"/>, or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentException">Outer agent, session, branch, run, or turn coordinates differ from the captured evidence, or <paramref name="tools"/> is a default, uninitialized array.</exception>
    public ContextAssemblyRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        TurnId turnId,
        ModelRequestId modelRequestId,
        ModelDescriptor model,
        ContextAssemblyEvidence evidence,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(model);
        ValidateEvidence(agentId, sessionId, branchId, runId, turnId, evidence);
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
        Evidence = evidence;
        Tools = tools;
        ToolChoice = toolChoice;
        Settings = settings;
        Extensions = extensions;
    }

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

    /// <summary>Gets the atomic assembly evidence this request was built from.</summary>
    /// <value>The captured evidence; never null.</value>
    public ContextAssemblyEvidence Evidence { get; }

    /// <summary>Gets the eligible conversation history, in ascending commit order.</summary>
    /// <value>Exactly <see cref="HistoryView.Messages"/> of <see cref="ContextAssemblyEvidence.History"/>.</value>
    public ImmutableArray<AgentMessage> History => Evidence.History.Messages;

    /// <summary>Gets the tools available for the model to call.</summary>
    public ImmutableArray<LlmToolDefinition> Tools { get; init; }

    /// <summary>Gets the tool-call selection policy.</summary>
    public LlmToolChoice ToolChoice { get; init; }

    /// <summary>Gets the effective sampling and output settings.</summary>
    public LlmRequestSettings Settings { get; init; }

    /// <summary>Gets caller-specific or forward-compatible request data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <summary>Gets the output contract to include on the assembled provider request.</summary>
    /// <value>The selected definition, or <see langword="null"/> when the run has no structured output requirement.</value>
    public OutputDefinition? Output { get; init; }

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
        && Evidence.Equals(other.Evidence)
        && Tools.SequenceEqual(other.Tools)
        && ToolChoice.Equals(other.ToolChoice)
        && Settings.Equals(other.Settings)
        && Extensions.Equals(other.Extensions)
        && Equals(Output, other.Output);

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
        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(ToolChoice);
        hash.Add(Evidence);
        hash.Add(Settings);
        hash.Add(Extensions);
        hash.Add(Output);
        return hash.ToHashCode();
    }

    private static void ValidateEvidence(
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
    }
}
