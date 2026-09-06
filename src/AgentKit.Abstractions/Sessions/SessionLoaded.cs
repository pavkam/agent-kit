// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The session was found and its current descriptor returned.</summary>
public sealed record SessionLoaded: SessionLoadResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionLoaded"/> record.</summary>
    /// <param name="descriptor">The session's current descriptor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public SessionLoaded(SessionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    /// <summary>Gets the session's current descriptor.</summary>
    public SessionDescriptor Descriptor { get; init; }
}
