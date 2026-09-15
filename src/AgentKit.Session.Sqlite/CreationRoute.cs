// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Retains the complete immutable create request and its winning directory location.</summary>
internal sealed record CreationRoute
{
    /// <summary>Initializes replay evidence for one successful creation route.</summary>
    /// <param name="request">The complete non-null logical creation request.</param>
    /// <param name="location">The non-null authoritative location chosen for that request.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public CreationRoute(SessionCreateRequest request, SessionLocation location)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(location);
        Request = request;
        Location = location;
    }

    /// <summary>Gets the immutable creation evidence used for exact replay comparison.</summary>
    public SessionCreateRequest Request { get; }

    /// <summary>Gets the authoritative location returned to exact retries.</summary>
    public SessionLocation Location { get; }
}
