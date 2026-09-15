// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports one provider-exposed incremental reasoning fragment.</summary>
public sealed record ConversationReasoningEvent: ConversationEvent
{
    /// <summary>Initializes a reasoning-fragment event.</summary>
    /// <param name="text">The nonnull provider-exposed reasoning fragment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public ConversationReasoningEvent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    /// <summary>Gets the provider-exposed reasoning fragment.</summary>
    public string Text { get; }
}
