// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Captures one immutable, reproducible point in an agent session branch's
/// message history.
/// </summary>
/// <remarks>
/// A cursor carries the exact branch version and append sequence observed by
/// its creator. It is revalidation evidence, not a live store handle: creating
/// it performs no store lookup, authentication, coherence check, or authority
/// grant. A later append may make this cursor stale without changing it.
/// </remarks>
public sealed record MessageCursor
{
    /// <summary>
    /// Initializes a cursor for an exact message-history watermark.
    /// </summary>
    /// <param name="agentId">The nondefault agent that owns the session.</param>
    /// <param name="sessionId">The nondefault durable session identity.</param>
    /// <param name="conversationId">The optional nondefault conversation correlated with this history.</param>
    /// <param name="branchId">The nondefault branch whose history was observed.</param>
    /// <param name="version">The observed nonnegative optimistic-concurrency version.</param>
    /// <param name="sequence">The observed nonnegative append sequence.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/>, <paramref name="sessionId"/>,
    /// <paramref name="conversationId"/>, or <paramref name="branchId"/> is
    /// default.
    /// </exception>
    public MessageCursor(
        AgentId agentId,
        SessionId sessionId,
        ConversationId? conversationId,
        BranchId branchId,
        SessionVersion version,
        SessionSequence sequence)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        if (conversationId is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(conversationId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);

        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        BranchId = branchId;
        Version = version;
        Sequence = sequence;
    }

    /// <summary>Gets the agent that owns the observed session.</summary>
    /// <value>A nondefault agent identity.</value>
    public AgentId AgentId { get; }

    /// <summary>Gets the durable session whose branch was observed.</summary>
    /// <value>A nondefault session identity.</value>
    public SessionId SessionId { get; }

    /// <summary>Gets the optional conversation correlated with the history.</summary>
    /// <value>A nondefault conversation identity when present; otherwise <see langword="null"/>.</value>
    public ConversationId? ConversationId { get; }

    /// <summary>Gets the branch whose append position this cursor identifies.</summary>
    /// <value>A nondefault branch identity.</value>
    public BranchId BranchId { get; }

    /// <summary>Gets the exact observed branch version.</summary>
    /// <value>A nonnegative version; zero identifies an empty initial branch version.</value>
    public SessionVersion Version { get; }

    /// <summary>Gets the exact observed append sequence within <see cref="BranchId"/>.</summary>
    /// <value>A nonnegative sequence; zero identifies the position before the first append.</value>
    public SessionSequence Sequence { get; }
}
