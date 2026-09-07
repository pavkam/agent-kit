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
/// version, a <c>SecurityAuthorizationContext</c>, a hook dispatch context,
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
    /// <param name="model">The selected model descriptor.</param>
    /// <param name="instructions">The system and developer instructions to place first in every request.</param>
    /// <param name="tools">The tools available for the model to call during this run.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="maxTurns">The maximum number of turns this run may take before it is halted.</param>
    /// <param name="attemptTimeout">The maximum duration allowed for a single model attempt.</param>
    /// <param name="extensions">Caller-specific or forward-compatible request data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/>, <paramref name="model"/>,
    /// <paramref name="toolChoice"/>, <paramref name="settings"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="instructions"/> or <paramref name="tools"/> is a
    /// default, uninitialized array.
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
        ModelDescriptor model,
        ImmutableArray<AgentMessage> instructions,
        ImmutableArray<ChatToolDefinition> tools,
        ChatToolChoice toolChoice,
        ChatRequestSettings settings,
        int maxTurns,
        TimeSpan attemptTimeout,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(model);
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
        Model = model;
        Instructions = instructions;
        Tools = tools;
        ToolChoice = toolChoice;
        Settings = settings;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
        Extensions = extensions;
    }

    /// <summary>Gets the agent this run belongs to.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this run reads from and commits to.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the branch this run reads from and commits to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the stable identity of this run.</summary>
    public RunId RunId { get; init; }

    /// <summary>Gets the identity on whose behalf this run is performed.</summary>
    public ExecutionIdentity Identity { get; init; }

    /// <summary>Gets the selected model descriptor.</summary>
    public ModelDescriptor Model { get; init; }

    /// <summary>Gets the system and developer instructions to place first in every request.</summary>
    public ImmutableArray<AgentMessage> Instructions { get; init; }

    /// <summary>Gets the tools available for the model to call during this run.</summary>
    public ImmutableArray<ChatToolDefinition> Tools { get; init; }

    /// <summary>Gets the tool-call selection policy.</summary>
    public ChatToolChoice ToolChoice { get; init; }

    /// <summary>Gets the effective sampling and output settings.</summary>
    public ChatRequestSettings Settings { get; init; }

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
        && Model.Equals(other.Model)
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
        hash.Add(Model);
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
