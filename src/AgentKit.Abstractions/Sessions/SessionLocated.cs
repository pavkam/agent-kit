// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an authoritative location visible to the requesting tenant.</summary>
/// <remarks>The location is routing evidence only; a separately authorized store call is still required.</remarks>
public sealed record SessionLocated: SessionLocationResult
{
    /// <summary>Initializes a successful location outcome.</summary>
    /// <param name="location">The non-null authoritative location.</param>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public SessionLocated(SessionLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        Location = location;
    }

    /// <summary>Gets the visible authoritative location.</summary><value>The non-null tenant-partitioned route.</value>
    public SessionLocation Location { get; }
}
