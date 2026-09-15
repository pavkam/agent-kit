// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Reports the single terminal state of an observed conversational turn.</summary>
public sealed record ConversationTurnCompletedEvent: ConversationEvent
{
    /// <summary>Initializes a terminal turn event.</summary>
    /// <param name="succeeded">Whether the turn reached a successful settled outcome.</param>
    /// <param name="outcome">A nonblank, content-safe outcome label.</param>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is blank.</exception>
    public ConversationTurnCompletedEvent(bool succeeded, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        Succeeded = succeeded;
        Outcome = outcome;
    }

    /// <summary>Gets whether the turn reached a successful settled outcome.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the content-safe terminal outcome label.</summary>
    public string Outcome { get; }
}
