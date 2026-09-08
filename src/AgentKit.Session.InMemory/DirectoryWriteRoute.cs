// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Retains the complete immutable route write and the location it committed.</summary>
internal sealed record DirectoryWriteRoute
{
    /// <summary>Initializes exact replay evidence for one committed route write.</summary>
    /// <param name="request">The complete non-null write request.</param>
    /// <param name="location">The non-null location committed by the request.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    internal DirectoryWriteRoute(SessionDirectoryWriteRequest request, SessionLocation location)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(location);
        Request = request;
        Location = location;
    }

    /// <summary>Gets the immutable request used for exact replay comparison.</summary>
    internal SessionDirectoryWriteRequest Request { get; }
    /// <summary>Gets the authoritative location returned to exact retries.</summary>
    internal SessionLocation Location { get; }
}
