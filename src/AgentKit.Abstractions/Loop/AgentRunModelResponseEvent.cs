// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one provider-neutral event from a model attempt.</summary>
public sealed record AgentRunModelResponseEvent: AgentRunEvent
{
    /// <summary>Initializes an event for one model response observation.</summary>
    /// <param name="turnId">The turn containing the model attempt.</param>
    /// <param name="responseEvent">The ordered provider-neutral model event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="responseEvent"/> is null.</exception>
    public AgentRunModelResponseEvent(TurnId turnId, ModelResponseEvent responseEvent)
    {
        ArgumentNullException.ThrowIfNull(responseEvent);
        TurnId = turnId;
        ResponseEvent = responseEvent;
    }

    /// <summary>Gets the turn containing the model attempt.</summary>
    public TurnId TurnId { get; init; }

    /// <summary>Gets the original provider-neutral model event.</summary>
    public ModelResponseEvent ResponseEvent { get; init; }
}
