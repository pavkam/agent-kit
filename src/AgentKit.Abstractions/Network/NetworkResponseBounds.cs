// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Response streaming and redirect limits for one authorized network send.</summary>
public sealed record NetworkResponseBounds
{
    /// <summary>Initializes response-phase bounds.</summary>
    /// <param name="responseTimeout">The maximum time allowed to receive the complete response.</param>
    /// <param name="maximumResponseBytes">The maximum number of response body bytes accepted.</param>
    /// <param name="maximumRedirects">The maximum number of redirects followed before failing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="responseTimeout"/> is not positive, <paramref name="maximumResponseBytes"/> is not positive,
    /// or <paramref name="maximumRedirects"/> is negative.
    /// </exception>
    public NetworkResponseBounds(TimeSpan responseTimeout, long maximumResponseBytes, int maximumRedirects)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(responseTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRedirects);
        ResponseTimeout = responseTimeout;
        MaximumResponseBytes = maximumResponseBytes;
        MaximumRedirects = maximumRedirects;
    }

    /// <summary>Gets the maximum time allowed to receive the complete response.</summary>
    public TimeSpan ResponseTimeout { get; init; }

    /// <summary>Gets the maximum number of response body bytes accepted.</summary>
    public long MaximumResponseBytes { get; init; }

    /// <summary>Gets the maximum number of redirects followed before failing.</summary>
    public int MaximumRedirects { get; init; }
}
