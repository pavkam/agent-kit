// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests that the engine create a new session for one agent.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record AgentSessionCreateRequest
{
    /// <summary>Initializes a new instance of the <see cref="AgentSessionCreateRequest"/> record.</summary>
    /// <param name="agentId">The agent the new session belongs to.</param>
    /// <param name="identity">
    /// The already-authenticated identity that will own the created session. AgentKit consumes this identity; it
    /// never authenticates it.
    /// </param>
    /// <param name="conversationId">The optional conversation the new session correlates with.</param>
    /// <param name="idempotencyKey">
    /// The key making a retried creation attempt observably idempotent rather than creating a second session.
    /// </param>
    /// <param name="extensions">Caller-supplied forward-compatible session data.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="conversationId"/> (when present), or
    /// <paramref name="idempotencyKey"/> is default.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> or <paramref name="extensions"/> is <see langword="null"/>.
    /// </exception>
    public AgentSessionCreateRequest(
        AgentId agentId,
        ExecutionIdentity identity,
        ConversationId? conversationId,
        IdempotencyKey idempotencyKey,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        ArgumentNullException.ThrowIfNull(identity);
        if (conversationId is { } capturedConversationId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(capturedConversationId, default, nameof(conversationId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(idempotencyKey, default, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(extensions);

        AgentId = agentId;
        Identity = identity;
        ConversationId = conversationId;
        IdempotencyKey = idempotencyKey;
        Extensions = extensions;
    }

    /// <summary>Gets the agent the new session belongs to.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the identity that will own the created session.</summary>
    public ExecutionIdentity Identity { get; init; }

    /// <summary>Gets the optional conversation the new session correlates with.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>Gets the key making a retried creation attempt idempotent.</summary>
    public IdempotencyKey IdempotencyKey { get; init; }

    /// <summary>Gets caller-supplied forward-compatible session data.</summary>
    public ExtensionData Extensions { get; init; }
}
