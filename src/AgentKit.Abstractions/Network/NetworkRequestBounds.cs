// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Connection and upload limits for one authorized network send.</summary>
public sealed record NetworkRequestBounds
{
    /// <summary>Initializes request-phase bounds.</summary>
    /// <param name="connectTimeout">The maximum time allowed to connect to a pinned address.</param>
    /// <param name="maximumRequestBytes">The maximum request body bytes that may be transmitted.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="connectTimeout"/> is not positive, or <paramref name="maximumRequestBytes"/> is not positive.
    /// </exception>
    public NetworkRequestBounds(TimeSpan connectTimeout, long maximumRequestBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRequestBytes);
        ConnectTimeout = connectTimeout;
        MaximumRequestBytes = maximumRequestBytes;
    }

    /// <summary>Gets the maximum time allowed to connect to a pinned address.</summary>
    public TimeSpan ConnectTimeout { get; init; }

    /// <summary>Gets the maximum request body bytes that may be transmitted.</summary>
    public long MaximumRequestBytes { get; init; }
}
