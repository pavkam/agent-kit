// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The session was deleted, or an identical prior request with the same
/// idempotency key already deleted it.
/// </summary>
public sealed record SessionDeleted: SessionDeleteResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionDeleted"/> record.</summary>
    /// <param name="address">The deleted session's address.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public SessionDeleted(SessionAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    /// <summary>Gets the deleted session's address.</summary>
    public SessionAddress Address { get; init; }
}
