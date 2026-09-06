// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A session was created.</summary>
public sealed record SessionCreatedEvent: SessionEvent
{
    /// <summary>Initializes a new instance of the <see cref="SessionCreatedEvent"/> record.</summary>
    /// <param name="address">The session this event concerns.</param>
    /// <param name="occurredAt">The time this event occurred.</param>
    /// <param name="descriptor">The created session's descriptor.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="descriptor"/> (or a base parameter) is null.
    /// </exception>
    public SessionCreatedEvent(SessionAddress address, DateTimeOffset occurredAt, SessionDescriptor descriptor)
        : base(address, occurredAt)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    /// <summary>Gets the created session's descriptor.</summary>
    public SessionDescriptor Descriptor { get; init; }
}
