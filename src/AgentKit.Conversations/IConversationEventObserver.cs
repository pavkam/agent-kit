// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Receives ordered incremental activity while one conversational turn is running.</summary>
/// <remarks>
/// Events are a best-effort display projection. They do not replace the authoritative session history or the
/// terminal <see cref="ConversationTurnResult"/>. Observer failures are isolated and never repeat agent effects.
/// </remarks>
public interface IConversationEventObserver
{
    /// <summary>Observes the next incremental turn event.</summary>
    /// <param name="conversationEvent">The next display-oriented event.</param>
    /// <param name="cancellationToken">Cancels this bounded delivery when the turn is cancelled.</param>
    /// <returns>An operation that completes when this event has been consumed or intentionally dropped.</returns>
    public ValueTask OnEventAsync(
        ConversationEvent conversationEvent,
        CancellationToken cancellationToken = default);
}
