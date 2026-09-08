// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete, immutable request to run an agent loop for one run.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>AgentRunInvocation</c> described by the agent-runtime architecture,
/// which additionally carries an <c>AgentDefinition</c>, an agent catalog
/// version, a hook dispatch context,
/// a compiled <c>AgentRunServices</c> bundle, an effective-configuration
/// snapshot, and a run policy snapshot. Until those packages exist, this
/// request carries the model, tool, and turn-limit choices directly, and
/// the caller is responsible for admitting any new input by appending it to
/// the session before starting a run — there is no queued-input admission
/// boundary yet.
/// </para>
/// </remarks>
public sealed record AgentRunRequest
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunRequest"/> record.</summary>
    /// <param name="agentId">The agent this run belongs to.</param>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="branchId">The branch this run reads from and commits to.</param>
    /// <param name="runId">The stable identity of this run.</param>
    /// <param name="identity">The identity on whose behalf this run is performed.</param>
    /// <param name="authorization">The captured run-start authorization and configuration evidence.</param>
    /// <param name="sessionProfile">The immutable session profile selected for this invocation.</param>
    /// <param name="modelPolicy">The candidate and fallback policy used to choose this run's model.</param>
    /// <param name="modelRequirements">The portable behaviors this run's requests need.</param>
    /// <param name="instructions">The system and developer instructions to place first in every request.</param>
    /// <param name="tools">The tools available for the model to call during this run.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="maxTurns">The maximum number of turns this run may take before it is halted.</param>
    /// <param name="attemptTimeout">The maximum duration allowed for a single model attempt.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/>, <paramref name="authorization"/>,
    /// <paramref name="sessionProfile"/>, <paramref name="modelPolicy"/>,
    /// <paramref name="modelRequirements"/>, <paramref name="toolChoice"/>,
    /// <paramref name="settings"/>, or <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="instructions"/> or <paramref name="tools"/> is a
    /// default, uninitialized array, or <paramref name="authorization"/> does
    /// not exactly match the supplied agent, session, run, and complete identity.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxTurns"/> is not positive, or
    /// <paramref name="attemptTimeout"/> is not positive.
    /// </exception>
    public AgentRunRequest(
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        SessionProfileSnapshot sessionProfile,
        ModelSelectionPolicy modelPolicy,
        ModelRequirements modelRequirements,
        ImmutableArray<AgentMessage> instructions,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        int maxTurns,
        TimeSpan attemptTimeout,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfInvalidRunAuthorization(identity, agentId, sessionId, runId, authorization);
        ArgumentNullException.ThrowIfNull(sessionProfile);
        ArgumentNullException.ThrowIfNull(modelPolicy);
        ArgumentNullException.ThrowIfNull(modelRequirements);
        ArgumentException.ThrowIfDefault(instructions);
        ArgumentException.ThrowIfDefault(tools);
        ArgumentNullException.ThrowIfNull(toolChoice);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTurns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(extensions);

        AgentId = agentId;
        SessionId = sessionId;
        BranchId = branchId;
        RunId = runId;
        Identity = identity;
        Authorization = authorization;
        SessionProfile = sessionProfile;
        ModelPolicy = modelPolicy;
        ModelRequirements = modelRequirements;
        Instructions = instructions;
        Tools = tools;
        ToolChoice = toolChoice;
        Settings = settings;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
        Extensions = extensions;
    }

    /// <summary>Gets the agent this run belongs to.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session this run reads from and commits to.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the branch this run reads from and commits to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the stable identity of this run.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the identity on whose behalf this run is performed.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the captured run-start authorization evidence.</summary>
    /// <value>Immutable evidence matching this request's identity, agent, session, and run.</value>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the immutable session profile selected for this invocation.</summary>
    /// <value>The exact compiled profile supplied to session coordination.</value>
    public SessionProfileSnapshot SessionProfile { get; }

    /// <summary>Gets the candidate and fallback policy used to choose this run's model.</summary>
    /// <value>
    /// The run states which models it may use; the configured selector
    /// resolves that policy against the engine-wide catalog. The run does not
    /// name a concrete provider adapter.
    /// </value>
    public ModelSelectionPolicy ModelPolicy { get; init; }

    /// <summary>Gets the portable behaviors this run's requests need.</summary>
    /// <value>
    /// Used to reject or downgrade an incompatible model before any provider
    /// request is sent.
    /// </value>
    public ModelRequirements ModelRequirements { get; init; }

    /// <summary>Gets the system and developer instructions to place first in every request.</summary>
    public ImmutableArray<AgentMessage> Instructions { get; init; }

    /// <summary>Gets the tools available for the model to call during this run.</summary>
    public ImmutableArray<LlmToolDefinition> Tools { get; init; }

    /// <summary>Gets the tool-call selection policy.</summary>
    public LlmToolChoice ToolChoice { get; init; }

    /// <summary>Gets the effective sampling and output settings.</summary>
    public LlmRequestSettings Settings { get; init; }

    /// <summary>Gets the maximum number of turns this run may take before it is halted.</summary>
    public int MaxTurns { get; init; }

    /// <summary>Gets the maximum duration allowed for a single model attempt.</summary>
    public TimeSpan AttemptTimeout { get; init; }

    /// <summary>Gets caller-specific or forward-compatible request data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(AgentRunRequest? other) =>
        other is not null
        && AgentId.Equals(other.AgentId)
        && SessionId.Equals(other.SessionId)
        && BranchId.Equals(other.BranchId)
        && RunId.Equals(other.RunId)
        && Identity.Equals(other.Identity)
        && Authorization.Equals(other.Authorization)
        && SessionProfile.Equals(other.SessionProfile)
        && ModelPolicy.Equals(other.ModelPolicy)
        && ModelRequirements.Equals(other.ModelRequirements)
        && Instructions.SequenceEqual(other.Instructions)
        && Tools.SequenceEqual(other.Tools)
        && ToolChoice.Equals(other.ToolChoice)
        && Settings.Equals(other.Settings)
        && MaxTurns == other.MaxTurns
        && AttemptTimeout == other.AttemptTimeout
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(SessionId);
        hash.Add(BranchId);
        hash.Add(RunId);
        hash.Add(Identity);
        hash.Add(Authorization);
        hash.Add(SessionProfile);
        hash.Add(ModelPolicy);
        hash.Add(ModelRequirements);
        foreach (var message in Instructions)
        {
            hash.Add(message);
        }

        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(ToolChoice);
        hash.Add(Settings);
        hash.Add(MaxTurns);
        hash.Add(AttemptTimeout);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
