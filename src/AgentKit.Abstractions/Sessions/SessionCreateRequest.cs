// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A request to create one new session, or to idempotently return an
/// existing one created by an identical prior request.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record SessionCreateRequest
{
    /// <summary>Initializes a new instance of the <see cref="SessionCreateRequest"/> record.</summary>
    /// <param name="agentId">The agent that will own the session.</param>
    /// <param name="identity">The identity creating the session.</param>
    /// <param name="conversationId">The optional higher-level conversation this session belongs to.</param>
    /// <param name="idempotencyKey">
    /// The key that makes repeating this exact request safe: a retry with
    /// the same key returns the originally created session rather than
    /// creating a duplicate.
    /// </param>
    /// <param name="extensions">Store-specific or forward-compatible creation data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public SessionCreateRequest(
        AgentId agentId,
        ExecutionIdentity identity,
        ConversationId? conversationId,
        IdempotencyKey idempotencyKey,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(extensions);

        AgentId = agentId;
        Identity = identity;
        ConversationId = conversationId;
        IdempotencyKey = idempotencyKey;
        Extensions = extensions;
    }

    /// <summary>Gets the agent that will own the session.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets the identity creating the session.</summary>
    public ExecutionIdentity Identity { get; init; }

    /// <summary>Gets the optional higher-level conversation this session belongs to.</summary>
    public ConversationId? ConversationId { get; init; }

    /// <summary>
    /// Gets the key that makes repeating this exact request safe.
    /// </summary>
    public IdempotencyKey IdempotencyKey { get; init; }

    /// <summary>Gets store-specific or forward-compatible creation data.</summary>
    public ExtensionData Extensions { get; init; }
}
