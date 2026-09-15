// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Records projected conversation events in delivery order.</summary>
internal sealed class RecordingConversationEventObserver: IConversationEventObserver
{
    /// <summary>Gets observed events in delivery order.</summary>
    internal List<ConversationEvent> Events { get; } = [];

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ConversationEvent conversationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationEvent);
        Events.Add(conversationEvent);
        return ValueTask.CompletedTask;
    }
}
