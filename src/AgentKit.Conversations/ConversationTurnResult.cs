// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Represents the outcome of one conversational turn submitted through <see cref="IConversationSession"/>.</summary>
public sealed record ConversationTurnResult
{
    /// <summary>Initializes a turn result.</summary>
    /// <param name="succeeded">Whether the user's message was durably recorded and the agent loop ran to a settled outcome.</param>
    /// <param name="events">The ordered, non-default rendered activity for this turn.</param>
    /// <exception cref="ArgumentException"><paramref name="events"/> is a default, uninitialized array.</exception>
    public ConversationTurnResult(bool succeeded, ImmutableArray<ConversationEvent> events)
    {
        ArgumentException.ThrowIfDefault(events);
        Succeeded = succeeded;
        Events = events;
    }

    /// <summary>Gets whether the user's message was durably recorded and the agent loop ran to a settled outcome.</summary>
    /// <value><see langword="false"/> when session admission itself failed; <see cref="Events"/> then carries a single explanatory event.</value>
    public bool Succeeded { get; init; }

    /// <summary>Gets the ordered rendered activity for this turn.</summary>
    /// <value>Never a default array; empty only when the underlying run committed no renderable content.</value>
    public ImmutableArray<ConversationEvent> Events { get; init; }
}
