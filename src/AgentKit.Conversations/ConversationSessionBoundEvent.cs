// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>
/// Announces to a live observer, once per conversation, the durable session and active branch every turn of that
/// conversation is recorded against.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IConversationSession"/> creates its underlying session lazily on the first <c>SendAsync</c> call, or
/// binds to an existing one through <c>OpenAsync</c>. The first turn observed through an
/// <see cref="IConversationEventObserver"/> begins with this event, before any assistant, tool, usage, or completion
/// event, so a streaming host can hand the <see cref="SessionId"/> to its client as a resume token before content
/// arrives. Later observed turns of the same conversation do not repeat it.
/// </para>
/// <para>
/// It is not part of <see cref="ConversationTurnResult.Events"/>, which holds only rendered turn activity; the
/// durable result exposes the same identities through <see cref="ConversationTurnResult.SessionId"/> and
/// <see cref="ConversationTurnResult.RunId"/>, and the conversation exposes them through
/// <see cref="IConversationSession.SessionId"/> and <see cref="IConversationSession.BranchId"/>.
/// </para>
/// </remarks>
public sealed record ConversationSessionBoundEvent: ConversationEvent
{
    /// <summary>Initializes a session-bound event.</summary>
    /// <param name="sessionId">The durable session identity the conversation is bound to.</param>
    /// <param name="branchId">The active branch every turn appends to.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> or <paramref name="branchId"/> is the default, uninitialized identity.
    /// </exception>
    public ConversationSessionBoundEvent(SessionId sessionId, BranchId branchId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);
        SessionId = sessionId;
        BranchId = branchId;
    }

    /// <summary>Gets the durable session identity the conversation is bound to.</summary>
    /// <value>Never the default identity.</value>
    public SessionId SessionId { get; }

    /// <summary>Gets the active branch every turn of the conversation appends to.</summary>
    /// <value>Never the default identity.</value>
    public BranchId BranchId { get; }
}
