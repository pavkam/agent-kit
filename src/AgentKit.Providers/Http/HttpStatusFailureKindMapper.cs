// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

using System.Net;

/// <summary>
/// Maps an HTTP response status code onto the portable
/// <see cref="ProviderFailureKind"/> taxonomy using one canonical,
/// provider-neutral table shared by every first-party HTTP provider adapter.
/// </summary>
/// <remarks>
/// <para>
/// The canonical table is:
/// </para>
/// <list type="table">
/// <listheader><term>Status</term><description>Kind</description></listheader>
/// <item><term>401</term><description><see cref="ProviderFailureKind.Authentication"/></description></item>
/// <item><term>403</term><description><see cref="ProviderFailureKind.Authorization"/></description></item>
/// <item><term>429</term><description><see cref="ProviderFailureKind.Throttling"/></description></item>
/// <item><term>408, 504</term><description><see cref="ProviderFailureKind.Timeout"/></description></item>
/// <item><term>413</term><description><see cref="ProviderFailureKind.InvalidRequest"/></description></item>
/// <item><term>529</term><description><see cref="ProviderFailureKind.Unavailable"/></description></item>
/// <item><term>other 4xx</term><description><see cref="ProviderFailureKind.InvalidRequest"/></description></item>
/// <item><term>other 5xx</term><description><see cref="ProviderFailureKind.Unavailable"/></description></item>
/// <item><term>1xx, 2xx, 3xx</term><description><see cref="ProviderFailureKind.ProtocolViolation"/></description></item>
/// <item><term>anything else</term><description><see cref="ProviderFailureKind.Unknown"/></description></item>
/// </list>
/// <para>
/// <c>504 Gateway Timeout</c> is classified as <see cref="ProviderFailureKind.Timeout"/>
/// rather than <see cref="ProviderFailureKind.Unavailable"/>: an upstream
/// deadline elapsed while the request was being served, which is the same
/// retry and budget signal as a <c>408</c>, and callers that budget by
/// elapsed time should treat both alike. <c>529</c> is the de facto
/// "overloaded" status used by several model vendors and is transient.
/// <c>413</c> is a caller fault (the payload exceeds the provider's limit)
/// and is never transient.
/// </para>
/// <para>
/// A <c>1xx</c>, <c>2xx</c>, or <c>3xx</c> status only reaches this mapper
/// when the adapter has already decided the response is not a usable
/// success (for example, a redirect that the adapter deliberately does not
/// follow, or an informational status that leaked through the transport).
/// Such a response is a wire-protocol anomaly rather than a caller or
/// provider fault, so it is reported as
/// <see cref="ProviderFailureKind.ProtocolViolation"/>. Values outside
/// <c>100</c>–<c>599</c> are not valid HTTP status codes and map to
/// <see cref="ProviderFailureKind.Unknown"/>.
/// </para>
/// <para>
/// Body-driven vocabularies such as Anthropic's <c>error.type</c>,
/// Google's <c>error.status</c>, or Bedrock's <c>x-amzn-errortype</c> are
/// provider-specific and remain in the owning adapter; adapters normally
/// consult those first and fall back to this table when the body yields
/// <see cref="ProviderFailureKind.Unknown"/>. Genuinely provider-specific
/// status semantics (nonstandard codes, or a standard code the provider
/// documents with a different meaning) are expressed through the
/// <c>overrides</c> parameter of
/// <see cref="Map(HttpStatusCode, IReadOnlyDictionary{HttpStatusCode, ProviderFailureKind}?)"/>
/// so the canonical table stays the single source of truth for everything
/// else.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class HttpStatusFailureKindMapper
{
    /// <summary>Maps an HTTP status code onto a normalized failure kind using the canonical table.</summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <returns>The normalized failure kind for <paramref name="statusCode"/>, as documented in the type remarks.</returns>
    public static ProviderFailureKind Map(HttpStatusCode statusCode) => Map(statusCode, overrides: null);

    /// <summary>
    /// Maps an HTTP status code onto a normalized failure kind, consulting a
    /// provider-specific override table before the canonical table.
    /// </summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <param name="overrides">
    /// Provider-specific exact-status overrides, or <see langword="null"/>
    /// when the provider has none. When <paramref name="statusCode"/> is
    /// present as a key, its value is returned without consulting the
    /// canonical table. The dictionary is read once per call and is never
    /// retained; callers should supply an immutable or frozen instance so
    /// the mapping stays deterministic.
    /// </param>
    /// <returns>
    /// The override kind when <paramref name="overrides"/> contains
    /// <paramref name="statusCode"/>; otherwise the canonical kind
    /// documented in the type remarks.
    /// </returns>
    public static ProviderFailureKind Map(
        HttpStatusCode statusCode,
        IReadOnlyDictionary<HttpStatusCode, ProviderFailureKind>? overrides) =>
        overrides is not null && overrides.TryGetValue(statusCode, out var overriddenKind)
            ? overriddenKind
            : MapCanonical(statusCode);

    private static ProviderFailureKind MapCanonical(HttpStatusCode statusCode) =>
        (int) statusCode switch
        {
            (int) HttpStatusCode.Unauthorized => ProviderFailureKind.Authentication,
            (int) HttpStatusCode.Forbidden => ProviderFailureKind.Authorization,
            (int) HttpStatusCode.TooManyRequests => ProviderFailureKind.Throttling,
            (int) HttpStatusCode.RequestTimeout or (int) HttpStatusCode.GatewayTimeout => ProviderFailureKind.Timeout,
            (int) HttpStatusCode.RequestEntityTooLarge => ProviderFailureKind.InvalidRequest,
            529 => ProviderFailureKind.Unavailable,
            >= 400 and < 500 => ProviderFailureKind.InvalidRequest,
            >= 500 and < 600 => ProviderFailureKind.Unavailable,
            >= 100 and < 400 => ProviderFailureKind.ProtocolViolation,
            _ => ProviderFailureKind.Unknown,
        };
}
