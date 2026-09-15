// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

using System.Collections.Frozen;
using System.Net;

using AgentKit.Providers.Http;

/// <summary>
/// Maps a Cohere HTTP error status code onto the normalized
/// <see cref="ProviderFailureKind"/> taxonomy.
/// </summary>
/// <remarks>
/// <para>
/// Cohere's error contract is a flat <c>{"message": "..."}</c> body with no
/// canonical machine-readable error-type vocabulary, so this mapping is
/// HTTP-status-driven: the shared
/// <see cref="HttpStatusFailureKindMapper"/> table applies, layered with
/// the Cohere-specific status semantics below.
/// </para>
/// <para>
/// Cohere documents <c>498</c> as an invalid-token response (an
/// authentication failure, not a generic client error) and <c>402</c> as a
/// billing or trial-key limitation on the account (an authorization
/// failure for the requested operation). It also returns
/// <c>501 Not Implemented</c> when the request asks for a feature the
/// endpoint does not support, which is a caller fault rather than a
/// transient outage. Cohere's nonstandard <c>499</c> means the client or
/// an intermediary closed the request; it is not mapped to
/// <see cref="ProviderFailureKind.Cancellation"/> because a server-reported
/// status can never prove that the AgentKit caller cancelled, so it falls
/// through to the shared 4xx default.
/// </para>
/// </remarks>
internal static class CohereErrorMapping
{
    /// <summary>Cohere-specific HTTP status semantics layered over the shared canonical table.</summary>
    private static readonly FrozenDictionary<HttpStatusCode, ProviderFailureKind> _statusOverrides =
        new Dictionary<HttpStatusCode, ProviderFailureKind>
        {
            [(HttpStatusCode) 498] = ProviderFailureKind.Authentication,
            [HttpStatusCode.PaymentRequired] = ProviderFailureKind.Authorization,
            [HttpStatusCode.NotImplemented] = ProviderFailureKind.InvalidRequest,
        }.ToFrozenDictionary();

    /// <summary>Maps an HTTP status code onto a normalized failure kind.</summary>
    /// <param name="statusCode">The HTTP status code the provider returned.</param>
    /// <returns>
    /// The Cohere-specific override for <paramref name="statusCode"/> when
    /// one exists; otherwise the shared canonical mapping from
    /// <see cref="HttpStatusFailureKindMapper"/>.
    /// </returns>
    public static ProviderFailureKind MapStatusCode(HttpStatusCode statusCode) =>
        HttpStatusFailureKindMapper.Map(statusCode, _statusOverrides);
}
