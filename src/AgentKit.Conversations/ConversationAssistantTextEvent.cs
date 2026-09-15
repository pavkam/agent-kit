// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports one complete assistant-authored text part committed during a turn.</summary>
public sealed record ConversationAssistantTextEvent: ConversationEvent
{
    /// <summary>Initializes an assistant-text event.</summary>
    /// <param name="text">The nonblank assistant-authored text.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    public ConversationAssistantTextEvent(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }

    /// <summary>Gets the assistant-authored text.</summary>
    public string Text { get; init; }
}
