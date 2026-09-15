// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns one ordered page of visible authoritative session routes.</summary>
public sealed record SessionDirectoryPage: SessionDirectoryListResult
{
    /// <summary>Initializes a directory page.</summary>
    /// <param name="locations">The initialized page ordered by session identity.</param>
    /// <param name="nextCursor">The last returned identity when another page exists.</param>
    /// <exception cref="ArgumentException"><paramref name="locations"/> is default.</exception>
    public SessionDirectoryPage(ImmutableArray<SessionLocation> locations, SessionId? nextCursor)
    { ArgumentException.ThrowIfDefault(locations); Locations = locations; NextCursor = nextCursor; }
    /// <summary>Gets the visible routes.</summary><value>An initialized, possibly empty array.</value>
    public ImmutableArray<SessionLocation> Locations { get; }
    /// <summary>Gets the continuation cursor.</summary><value>Null when no later route is known.</value>
    public SessionId? NextCursor { get; }
}
