// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete, immutable request to run an agent through the process-level facade.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its fields. It carries no mutable state
/// and is safe to share across threads without synchronization.
/// </para>
/// <para>
/// This is the facade-level request the documented <c>Agent.RunAsync&lt;TOutput&gt;</c>/<c>StreamAsync&lt;TOutput&gt;</c>
/// surface accepts (<c>agent-runtime.md</c>/<c>composition-and-configuration.md:274-313</c>); it is distinct from
/// the reduced <see cref="AgentLoopRunRequest"/> the loop itself consumes, which additionally carries the pinned
/// definition, security authorization, and every model/tool/turn setting the loop needs. This type carries only
/// what a caller supplies before the engine compiles that fuller evidence.
/// </para>
/// </remarks>
public sealed record AgentRunRequest
{
    private readonly AgentInput _input;

    /// <summary>Initializes a new instance of the <see cref="AgentRunRequest"/> record.</summary>
    /// <param name="agentId">The agent to run.</param>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="conversationId">The optional conversation this run correlates with.</param>
    /// <param name="identity">
    /// The already-authenticated identity on whose behalf the run is performed. AgentKit consumes this identity;
    /// it never authenticates it.
    /// </param>
    /// <param name="input">The admitted input this run processes.</param>
    /// <param name="options">The bounded overrides for this invocation, or <see langword="null"/> for none.</param>
    /// <param name="executionLaneId">
    /// The execution lane this run must observe and advance, or <see langword="null"/> to derive one from the
    /// session.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="sessionId"/>, a present <paramref name="conversationId"/>, or a
    /// present <paramref name="executionLaneId"/> is default.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> or <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    public AgentRunRequest(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        AgentInput input,
        AgentRunOptions? options = null,
        ExecutionLaneId? executionLaneId = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        if (conversationId is { } capturedConversationId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(capturedConversationId, default, nameof(conversationId));
        }

        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(input);
        if (executionLaneId is { } capturedExecutionLaneId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(capturedExecutionLaneId, default, nameof(executionLaneId));
        }

        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        Identity = identity;
        _input = input;
        Options = options;
        ExecutionLaneId = executionLaneId;
    }

    /// <summary>Gets the agent to run.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the session this run reads from and commits to.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the optional conversation this run correlates with.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets the authenticated identity the run is performed for.</summary>
    public ExecutionIdentity Identity { get; init; }

    /// <summary>Gets the admitted input this run processes.</summary>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public AgentInput Input
    {
        get => _input;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Input));
            _input = value;
        }
    }

    /// <summary>Gets the bounded overrides for this invocation, or <see langword="null"/> for none.</summary>
    public AgentRunOptions? Options { get; init; }

    /// <summary>
    /// Gets the execution lane this run must observe and advance, or <see langword="null"/> to derive one from the
    /// session.
    /// </summary>
    public ExecutionLaneId? ExecutionLaneId { get; init; }
}
