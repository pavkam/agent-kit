// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The response body exceeded its configured maximum size while streaming.</summary>
/// <remarks>
/// The stream stopped at the bound; no truncated content is presented as a
/// successful response.
/// </remarks>
public sealed record NetworkResponseLimitExceeded: NetworkSendResult
{
    /// <summary>Initializes a new instance of the <see cref="NetworkResponseLimitExceeded"/> record.</summary>
    /// <param name="observedBytes">The number of bytes read before the bound was reached.</param>
    /// <param name="maximumBytes">The configured maximum response size.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="observedBytes"/> or <paramref name="maximumBytes"/> is negative.
    /// </exception>
    public NetworkResponseLimitExceeded(long observedBytes, long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);

        ObservedBytes = observedBytes;
        MaximumBytes = maximumBytes;
    }

    /// <summary>Gets the number of bytes read before the bound was reached.</summary>
    public long ObservedBytes { get; init; }

    /// <summary>Gets the configured maximum response size.</summary>
    public long MaximumBytes { get; init; }
}
