// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Represents one piece of conversational-turn activity, in the order the turn committed it.</summary>
/// <remarks>
/// This is a closed hierarchy: <see cref="ConversationAssistantTextEvent"/>, <see cref="ConversationToolCallEvent"/>,
/// <see cref="ConversationToolResultEvent"/>, and <see cref="ConversationUsageEvent"/> are its only cases. Each
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
