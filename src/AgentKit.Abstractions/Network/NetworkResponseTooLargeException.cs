// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Signals that streamed bytes exceeded the request's enforced response boundary.</summary>
public sealed class NetworkResponseTooLargeException: IOException
{
    /// <summary>Initializes a response-size exception.</summary>
    /// <param name="maximumBytes">The positive configured maximum.</param>
    /// <param name="observedBytes">The minimum observed byte count greater than the maximum.</param>
    /// <exception cref="ArgumentOutOfRangeException">A byte count is invalid.</exception>
    public NetworkResponseTooLargeException(long maximumBytes, long observedBytes)
        : base($"The response exceeded the configured {maximumBytes}-byte boundary.")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(observedBytes, maximumBytes);
        MaximumBytes = maximumBytes;
        ObservedBytes = observedBytes;
    }

    /// <summary>Gets the configured maximum bytes.</summary>
    public long MaximumBytes { get; }

    /// <summary>Gets the minimum observed bytes.</summary>
    public long ObservedBytes { get; }
}
