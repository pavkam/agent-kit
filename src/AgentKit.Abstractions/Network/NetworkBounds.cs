// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The deadlines, response-size limit, and redirect limit that apply to one
/// resolution or request operation.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </para>
/// <para>
/// This is a deliberately consolidated stand-in for the fuller
/// architecture's separate resolution, request, and response bound types;
/// one shape covers every operation in this reduced package rather than
/// three narrowly scoped ones.
/// </para>
/// </remarks>
public sealed record NetworkBounds
{
    /// <summary>Initializes a new instance of the <see cref="NetworkBounds"/> record.</summary>
    /// <param name="connectTimeout">The maximum time allowed to resolve and connect.</param>
    /// <param name="responseTimeout">The maximum time allowed to receive the complete response.</param>
    /// <param name="maximumResponseBytes">The maximum number of response body bytes accepted.</param>
    /// <param name="maximumRedirects">The maximum number of redirects followed before failing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="connectTimeout"/> or <paramref name="responseTimeout"/> is not positive, or
    /// <paramref name="maximumResponseBytes"/> is not positive, or <paramref name="maximumRedirects"/> is negative.
    /// </exception>
    public NetworkBounds(
        TimeSpan connectTimeout, TimeSpan responseTimeout, long maximumResponseBytes, int maximumRedirects)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(responseTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRedirects);

        ConnectTimeout = connectTimeout;
        ResponseTimeout = responseTimeout;
        MaximumResponseBytes = maximumResponseBytes;
        MaximumRedirects = maximumRedirects;
    }

    /// <summary>Gets the maximum time allowed to resolve and connect.</summary>
    public TimeSpan ConnectTimeout { get; init; }

    /// <summary>Gets the maximum time allowed to receive the complete response.</summary>
    public TimeSpan ResponseTimeout { get; init; }

    /// <summary>Gets the maximum number of response body bytes accepted.</summary>
    public long MaximumResponseBytes { get; init; }

    /// <summary>Gets the maximum number of redirects followed before failing.</summary>
    public int MaximumRedirects { get; init; }
}
