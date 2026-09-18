// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Represents one piece of conversational-turn activity, in the order the turn committed it.</summary>
/// <remarks>
/// This is a closed hierarchy containing the one-time session binding, assistant text, exposed reasoning,
/// correlated tool lifecycle, usage, and turn-completion cases. Each
/// case carries only the loss-aware, display-oriented projection of the underlying <c>AgentMessage</c> content a
/// host needs to render a turn; it is not a substitute for the authoritative session history
/// <see cref="IConversationSession"/> commits.
/// </remarks>
public abstract record ConversationEvent
{
    private protected ConversationEvent()
    {
    }
}
