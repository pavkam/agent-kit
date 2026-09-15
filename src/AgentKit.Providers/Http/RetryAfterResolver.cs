// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

using System.Net.Http.Headers;

/// <summary>
/// Resolves an HTTP <c>Retry-After</c> response header into the normalized,
/// non-negative delay carried by <see cref="ProviderFailure.RetryAfter"/>.
/// </summary>
/// <remarks>
/// <para>
/// RFC 9110 permits two <c>Retry-After</c> forms: a delay in whole seconds
/// (<c>Retry-After: 30</c>) and an absolute HTTP-date
/// (<c>Retry-After: Wed, 21 Oct 2026 07:28:00 GMT</c>). The delay form is
/// returned as-is. The date form is converted to a delay relative to the
/// injected <see cref="TimeProvider"/>'s current UTC instant, never the
/// ambient system clock, so adapters remain deterministic in tests.
/// </para>
/// <para>
/// The result is always non-negative: an HTTP-date already in the past, or
/// a clock skew that produces a negative remainder, resolves to
/// <see cref="TimeSpan.Zero"/> meaning "retry is permitted now". A missing
/// header, or one whose value the BCL header parser could not interpret,
/// resolves to <see langword="null"/> meaning "the provider gave no
/// guidance". The value describes what the provider said; it never
/// instructs a caller to retry.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class RetryAfterResolver
{
    /// <summary>Resolves the <c>Retry-After</c> header, if present and parseable, into a non-negative delay.</summary>
    /// <param name="headers">The response headers to inspect.</param>
    /// <param name="timeProvider">The clock used to convert an absolute HTTP-date into a relative delay.</param>
    /// <returns>
    /// The provider's requested delay, clamped to a minimum of
    /// <see cref="TimeSpan.Zero"/>; or <see langword="null"/> when
    /// <paramref name="headers"/> carries no usable <c>Retry-After</c>
    /// value.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public static TimeSpan? Resolve(HttpResponseHeaders headers, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var retryAfter = headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter.Date is { } date)
        {
            var remaining = date - timeProvider.GetUtcNow();
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        return null;
    }
}
