// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a visible route recorded for an equivalent prior creation request.</summary>
/// <remarks>The route is authoritative replay evidence. The caller must not allocate another session identity and must finish idempotent creation against this exact location.</remarks>
public sealed record SessionCreationLocationLocated: SessionCreationLocationResult
{
    /// <summary>Initializes a successful creation-route lookup result.</summary>
    /// <param name="location">The non-null tenant-visible authoritative location.</param>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public SessionCreationLocationLocated(SessionLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        Location = location;
    }

    /// <summary>Gets the prior creation route.</summary>
    /// <value>The non-null location that must be returned for an idempotent replay.</value>
    public SessionLocation Location { get; }
}
