// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports one incremental assistant-text fragment exactly as streamed by the provider.</summary>
public sealed record ConversationAssistantTextDeltaEvent: ConversationEvent
{
    /// <summary>Initializes an incremental assistant-text event.</summary>
    /// <param name="text">The nonnull text fragment, which may contain only whitespace.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public ConversationAssistantTextDeltaEvent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the exact incremental text fragment.</summary>
    public string Text { get; }
}
