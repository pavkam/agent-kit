// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The request followed more redirects than its configured maximum before reaching a terminal response.</summary>
public sealed record NetworkRedirectLimitExceeded: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkRedirectLimitExceeded"/> record.</summary>
    /// <param name="maximumRedirects">The configured maximum number of redirects.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumRedirects"/> is negative.</exception>
    public NetworkRedirectLimitExceeded(int maximumRedirects)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRedirects);
        MaximumRedirects = maximumRedirects;
    }

    /// <summary>Gets the configured maximum number of redirects.</summary>
    public int MaximumRedirects { get; init; }
}
