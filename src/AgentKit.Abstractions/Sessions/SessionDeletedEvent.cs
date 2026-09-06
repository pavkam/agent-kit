// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A session and its entire record were deleted.</summary>
public sealed record SessionDeletedEvent: SessionEvent
{
    /// <summary>Initializes a new instance of the <see cref="SessionDeletedEvent"/> record.</summary>
    /// <param name="address">The session this event concerns.</param>
    /// <param name="occurredAt">The time this event occurred.</param>
    /// <exception cref="ArgumentNullException">A base parameter is null.</exception>
    public SessionDeletedEvent(SessionAddress address, DateTimeOffset occurredAt)
        : base(address, occurredAt)
    {
    }
}
