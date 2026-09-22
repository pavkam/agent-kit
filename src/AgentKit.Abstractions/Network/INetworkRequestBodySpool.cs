// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Opens one bounded, separately authorized request-body stream whose bytes were
/// staged before egress authority was granted.
/// </summary>
public interface INetworkRequestBodySpool
{
    /// <summary>
    /// Opens the staged body stream for one send. The stream is bounded by
    /// <paramref name="maximumBytes"/> and must match <paramref name="expectedFingerprint"/>.
    /// </summary>
    /// <param name="expectedFingerprint">The authorized body fingerprint.</param>
    /// <param name="maximumBytes">The maximum bytes that may be read from the stream.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A readable stream positioned at the start of staged content.</returns>
    /// <exception cref="ArgumentException"><paramref name="expectedFingerprint"/> is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    public ValueTask<Stream> OpenReadAsync(
        ContentHash expectedFingerprint,
        long maximumBytes,
        CancellationToken cancellationToken = default);
}
