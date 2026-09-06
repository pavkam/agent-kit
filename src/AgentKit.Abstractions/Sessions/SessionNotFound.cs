// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// No session exists at the requested address, or the caller's identity is
/// not authorized to know that it does.
/// </summary>
/// <remarks>
/// This outcome is deliberately used for both "does not exist" and
/// "exists, but you may not know that" so that cross-tenant or
/// cross-principal access never leaks session existence across an
/// authorization boundary.
/// </remarks>
public sealed record SessionNotFound: SessionLoadResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionNotFound"/> record.</summary>
    /// <param name="address">The requested session address.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public SessionNotFound(SessionAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    /// <summary>Gets the requested session address.</summary>
    public SessionAddress Address { get; init; }
}
