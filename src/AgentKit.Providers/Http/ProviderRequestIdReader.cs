// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

using System.Net.Http.Headers;

/// <summary>
/// Reads a provider-supplied request identifier from an HTTP response
/// header into a <see cref="ProviderRequestId"/>.
/// </summary>
/// <remarks>
/// <para>
/// Vendors disagree on the header name (<c>x-request-id</c>,
/// <c>request-id</c>, <c>x-amzn-RequestId</c>, and so on), so the adapter
/// names the header and this type owns the shared reading rule: header
/// lookup is case-insensitive per HTTP, the first value that is not empty
/// or whitespace wins, and an absent or blank header yields
/// <see langword="null"/> rather than a fabricated identity. A whitespace-only
/// value is treated as absent because <see cref="ProviderRequestId"/>
/// rejects it and no correlation value would be usable.
/// </para>
/// <para>
/// The returned identifier is untrusted external correlation data. It is
/// never substituted for AgentKit's own request identities.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class ProviderRequestIdReader
{
    /// <summary>Reads the first usable value of one named response header as a provider request identifier.</summary>
    /// <param name="headers">The response headers to inspect.</param>
    /// <param name="headerName">The provider's request-identifier header name, matched case-insensitively.</param>
    /// <returns>
    /// The first non-blank value of <paramref name="headerName"/> wrapped in
    /// a <see cref="ProviderRequestId"/>; or <see langword="null"/> when the
    /// header is absent or every value is blank.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="headerName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static ProviderRequestId? TryRead(HttpResponseHeaders headers, string headerName)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        return TryReadSingle(headers, headerName);
    }

    /// <summary>
    /// Reads the first usable value across several candidate header names,
    /// in the order given, as a provider request identifier.
    /// </summary>
    /// <param name="headers">The response headers to inspect.</param>
    /// <param name="headerNames">
    /// Candidate header names in priority order, each matched
    /// case-insensitively. The first name that yields a non-blank value
    /// wins; later names are not consulted.
    /// </param>
    /// <returns>
    /// The first non-blank value found across <paramref name="headerNames"/>
    /// wrapped in a <see cref="ProviderRequestId"/>; or <see langword="null"/>
    /// when no candidate header carries a usable value.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="headerNames"/> is empty.</exception>
    /// <exception cref="ArgumentException">An element of <paramref name="headerNames"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static ProviderRequestId? TryRead(HttpResponseHeaders headers, params ReadOnlySpan<string> headerNames)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentOutOfRangeException.ThrowIfZero(headerNames.Length, nameof(headerNames));

        foreach (var headerName in headerNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(headerName, nameof(headerNames));
        }

        foreach (var headerName in headerNames)
        {
            if (TryReadSingle(headers, headerName) is { } requestId)
            {
                return requestId;
            }
        }

        return null;
    }

    private static ProviderRequestId? TryReadSingle(HttpResponseHeaders headers, string headerName)
    {
        Debug.Assert(headers is not null, "The caller validates the header collection.");
        Debug.Assert(!string.IsNullOrWhiteSpace(headerName), "The caller validates the header name.");

        if (!headers.TryGetValues(headerName, out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return new ProviderRequestId(value);
            }
        }

        return null;
    }
}
