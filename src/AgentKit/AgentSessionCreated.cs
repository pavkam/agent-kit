// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A session now exists for the requested agent and identity.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record AgentSessionCreated: AgentSessionCreationResult
{
    /// <summary>Initializes a new instance of the <see cref="AgentSessionCreated"/> record.</summary>
    /// <param name="agentId">The agent the session belongs to.</param>
    /// <param name="sessionId">The identity of the session now available for turns.</param>
    /// <param name="conversationId">The optional conversation the session correlates with.</param>
    /// <param name="existing">
    /// Whether a retried creation attempt returned the session an earlier, idempotent attempt already created,
    /// rather than creating a new one.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="sessionId"/>, or <paramref name="conversationId"/> (when
    /// present) is default.
    /// </exception>
    public AgentSessionCreated(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        bool existing)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        if (conversationId is { } capturedConversationId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(capturedConversationId, default, nameof(conversationId));
        }

        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        Existing = existing;
    }

    /// <summary>Gets the agent the session belongs to.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the identity of the session now available for turns.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the optional conversation the session correlates with.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets whether this result returned an earlier idempotent attempt's session rather than a new one.</summary>
    public bool Existing { get; init; }
}
