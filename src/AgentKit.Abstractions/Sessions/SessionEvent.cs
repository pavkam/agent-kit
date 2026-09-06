// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one observable session lifecycle event.
/// </summary>
/// <remarks>
/// This hierarchy is intentionally open (its constructor is
/// <see langword="protected"/>, not <see langword="private protected"/>):
/// other packages that extend session behavior, such as compaction, define
/// their own event kinds rather than overloading an existing one with
/// unrelated data. Events are observations only; a sink cannot mutate the
/// operation that produced them or veto it after the fact.
/// </remarks>
public abstract record SessionEvent
{
    /// <summary>Initializes a new instance of the <see cref="SessionEvent"/> record.</summary>
    /// <param name="address">The session this event concerns.</param>
    /// <param name="occurredAt">
    /// The time this event occurred, from the injected
    /// <see cref="TimeProvider"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    protected SessionEvent(SessionAddress address, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the session this event concerns.</summary>
    public SessionAddress Address { get; init; }

    /// <summary>
    /// Gets the time this event occurred, from the injected
    /// <see cref="TimeProvider"/>.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }
}
