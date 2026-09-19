// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the complete run correlation a run's dependency-injection scope was created for.</summary>
/// <remarks>
/// <para>
/// The facade only finishes discovering a run's session address, active conversation, and allocated
/// <see cref="RunId"/> partway through admission — after the scope already exists and after collaborators
/// resolved from that same scope may already need to correlate their own work to it. This immutable value is
/// the facade's answer to that ordering problem for run-scoped collaborators outside the facade's own assembly,
/// most notably AgentKit.IO's <c>RunEventHub</c> factory, which stamps every <see cref="RunEvent"/> it accepts
/// with this exact correlation.
/// </para>
/// <para>
/// This type carries no service instance and grants no authority; it is read-only identity evidence, scoped to
/// exactly one run.
/// </para>
/// </remarks>
public sealed record RunScopeIdentity
{
    /// <summary>Initializes the complete correlation for one run's scope.</summary>
    /// <param name="agentId">The non-default identity of the agent driving this run.</param>
    /// <param name="sessionId">The non-default identity of the session this run executes within.</param>
    /// <param name="conversationId">The optional non-default conversation correlated with the session.</param>
    /// <param name="runId">The non-default identity of this run.</param>
    /// <exception cref="ArgumentOutOfRangeException">A required identity is default, or a supplied conversation identity is default.</exception>
    public RunScopeIdentity(AgentId agentId, SessionId sessionId, ConversationId? conversationId, RunId runId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        if (conversationId is { } conversation)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(conversation, default, nameof(conversationId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        RunId = runId;
    }

    /// <summary>Gets the identity of the agent driving this run.</summary>
    /// <value>A non-default agent identity.</value>
    public AgentId AgentId { get; }

    /// <summary>Gets the identity of the session this run executes within.</summary>
    /// <value>A non-default session identity.</value>
    public SessionId SessionId { get; }

    /// <summary>Gets the conversation correlated with the session, when one exists.</summary>
    /// <value>A non-default conversation identity, or <see langword="null"/>.</value>
    public ConversationId? ConversationId { get; }

    /// <summary>Gets the identity of this run.</summary>
    /// <value>A non-default run identity.</value>
    public RunId RunId { get; }
}
