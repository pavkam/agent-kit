// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The resolution, request, and response bounds that apply to one network
/// operation.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record NetworkBounds
{
    /// <summary>Default maximum request body bytes for legacy consolidated constructors.</summary>
    public const long DefaultMaximumRequestBytes = 32 * 1024 * 1024;

    /// <summary>Initializes split bounds for one operation.</summary>
    /// <param name="resolution">The resolution-phase bounds.</param>
    /// <param name="request">The request-phase bounds.</param>
    /// <param name="response">The response-phase bounds.</param>
    /// <exception cref="ArgumentNullException">A bound partition is null.</exception>
    public NetworkBounds(
        NetworkResolutionBounds resolution,
        NetworkRequestBounds request,
        NetworkResponseBounds response)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        Resolution = resolution;
        Request = request;
        Response = response;
    }

    /// <summary>
    /// Initializes consolidated bounds using one connect timeout for both
    /// resolution and connection phases.
    /// </summary>
    /// <param name="connectTimeout">The maximum time allowed to resolve and connect.</param>
    /// <param name="responseTimeout">The maximum time allowed to receive the complete response.</param>
    /// <param name="maximumResponseBytes">The maximum number of response body bytes accepted.</param>
    /// <param name="maximumRedirects">The maximum number of redirects followed before failing.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A timeout is not positive, <paramref name="maximumResponseBytes"/> is not positive,
    /// or <paramref name="maximumRedirects"/> is negative.
    /// </exception>
    public NetworkBounds(
        TimeSpan connectTimeout,
        TimeSpan responseTimeout,
        long maximumResponseBytes,
        int maximumRedirects)
        : this(
            new NetworkResolutionBounds(connectTimeout),
            new NetworkRequestBounds(connectTimeout, DefaultMaximumRequestBytes),
            new NetworkResponseBounds(responseTimeout, maximumResponseBytes, maximumRedirects))
    {
    }

    /// <summary>Gets the resolution-phase bounds.</summary>
    public NetworkResolutionBounds Resolution { get; init; }

    /// <summary>Gets the request-phase bounds.</summary>
    public NetworkRequestBounds Request { get; init; }

    /// <summary>Gets the response-phase bounds.</summary>
    public NetworkResponseBounds Response { get; init; }

    /// <summary>Gets the maximum time allowed to resolve and connect.</summary>
    public TimeSpan ConnectTimeout => Request.ConnectTimeout;

    /// <summary>Gets the maximum time allowed to receive the complete response.</summary>
    public TimeSpan ResponseTimeout => Response.ResponseTimeout;

    /// <summary>Gets the maximum number of response body bytes accepted.</summary>
    public long MaximumResponseBytes => Response.MaximumResponseBytes;

    /// <summary>Gets the maximum number of redirects followed before failing.</summary>
    public int MaximumRedirects => Response.MaximumRedirects;

    /// <summary>Gets the maximum request body bytes that may be transmitted.</summary>
    public long MaximumRequestBytes => Request.MaximumRequestBytes;
}
