// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// No session or branch exists at the requested address, or the caller's
/// identity is not authorized to know that it does.
/// </summary>
public sealed record SessionAppendNotFound: SessionAppendResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionAppendNotFound"/> record.</summary>
    /// <param name="address">The requested session address.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public SessionAppendNotFound(SessionAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        Address = address;
    }

    /// <summary>Gets the requested session address.</summary>
    public SessionAddress Address { get; init; }
}
