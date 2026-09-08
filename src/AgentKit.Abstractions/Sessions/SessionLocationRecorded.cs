// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a session location was recorded or matched an idempotent prior record.</summary>
/// <remarks>When <see cref="Existing"/> is true, no new routing effect occurred and the returned location is the original authoritative binding.</remarks>
public sealed record SessionLocationRecorded
    : SessionDirectoryWriteResult
{
    /// <summary>Initializes a successful location-record outcome.</summary>
    /// <param name="location">The non-null authoritative location.</param>
    /// <param name="existing">Whether a matching prior binding satisfied this request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is null.</exception>
    public SessionLocationRecorded(SessionLocation location, bool existing)
    {
        ArgumentNullException.ThrowIfNull(location);
        Location = location; Existing = existing;
    }

    /// <summary>Gets the authoritative recorded route.</summary><value>The non-null location committed or returned from the prior idempotent record.</value>
    public SessionLocation Location { get; }
    /// <summary>Gets whether the route already existed.</summary><value><see langword="true"/> when no new route was committed.</value>
    public bool Existing { get; }
}
