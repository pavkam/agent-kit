// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

using System.Net.Http.Headers;
using System.Text;

/// <summary>
/// The credential resolved successfully into one usable HTTP authentication
/// header, identified by <see cref="HeaderName"/> and carrying the secret
/// <see cref="HeaderValue"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HeaderValue"/> is credential material: a raw API key, or a
/// bearer token including its <c>Bearer</c> scheme prefix. In line with the
/// <see cref="ProviderCredential"/> contract, it must never be logged,
/// serialized, or placed in an event, exception, or diagnostic message.
/// This record therefore overrides <see cref="ToString"/> and
/// <see cref="PrintMembers"/> so that the synthesized textual form prints
/// only the header name and the fixed <see cref="RedactionMarker"/>; the
/// value is reachable only through the <see cref="HeaderValue"/> property
/// and through <see cref="Apply"/>.
/// </para>
/// <para>
/// Structural equality and hashing remain over both properties, matching
/// <see cref="ApiKeyProviderCredential"/> and
/// <see cref="OAuthTokenProviderCredential"/>: values are compared, not
/// rendered. Callers must still keep instances inside the request pipeline
/// that consumes them.
/// </para>
/// </remarks>
public sealed record ProviderAuthorizationGranted: ProviderAuthorizationResult
{
    /// <summary>
    /// The fixed text substituted for <see cref="HeaderValue"/> in
    /// <see cref="ToString"/> and <see cref="PrintMembers"/>.
    /// </summary>
    public const string RedactionMarker = "[REDACTED]";

    private const string _authorizationHeaderName = "Authorization";

    /// <summary>Initializes a new instance of the <see cref="ProviderAuthorizationGranted"/> record.</summary>
    /// <param name="headerName">The name of the authentication header to attach to the request, such as <c>Authorization</c> or <c>x-api-key</c>.</param>
    /// <param name="headerValue">The complete value of the authentication header, including any scheme prefix such as <c>Bearer </c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> is
    /// empty or consists only of whitespace.
    /// </exception>
    public ProviderAuthorizationGranted(string headerName, string headerValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerValue);

        HeaderName = headerName;
        HeaderValue = headerValue;
    }

    /// <summary>Gets the name of the authentication header to attach to the request.</summary>
    public string HeaderName { get; init; }

    /// <summary>
    /// Gets the complete value of the authentication header to attach to the
    /// request. This is credential material and is excluded from
    /// <see cref="ToString"/>.
    /// </summary>
    public string HeaderValue { get; init; }

    /// <summary>
    /// Attaches this authentication header to <paramref name="headers"/>.
    /// </summary>
    /// <param name="headers">The request headers of the outgoing HTTP request.</param>
    /// <remarks>
    /// When <see cref="HeaderName"/> is the standard <c>Authorization</c>
    /// header and <see cref="HeaderValue"/> parses as a scheme-and-parameter
    /// <see cref="AuthenticationHeaderValue"/>, the typed
    /// <see cref="HttpRequestHeaders.Authorization"/> property is set so
    /// callers and test doubles can read the scheme and parameter back
    /// through the BCL's typed accessor. Every other header, and an
    /// <c>Authorization</c> value the BCL parser cannot interpret, is added
    /// verbatim through
    /// <see cref="HttpHeaders.TryAddWithoutValidation(string, string)"/>
    /// so the provider receives exactly the bytes the credential supplied.
    /// The wire representation is identical in both paths.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    public void Apply(HttpRequestHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (string.Equals(HeaderName, _authorizationHeaderName, StringComparison.OrdinalIgnoreCase)
            && AuthenticationHeaderValue.TryParse(HeaderValue, out var authorization))
        {
            headers.Authorization = authorization;
            return;
        }

        _ = headers.TryAddWithoutValidation(HeaderName, HeaderValue);
    }

    /// <summary>
    /// Returns a textual form that names the header but redacts its value.
    /// </summary>
    /// <returns>
    /// <c>ProviderAuthorizationGranted { HeaderName = &lt;name&gt;, HeaderValue = [REDACTED] }</c>.
    /// The secret <see cref="HeaderValue"/> never appears in the result.
    /// </returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        _ = builder.Append(nameof(ProviderAuthorizationGranted)).Append(" { ");
        _ = PrintMembers(builder);
        _ = builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Appends the printable members to <paramref name="builder"/>, writing
    /// <see cref="HeaderName"/> verbatim and <see cref="RedactionMarker"/>
    /// in place of <see cref="HeaderValue"/>.
    /// </summary>
    /// <param name="builder">The builder receiving the member text.</param>
    /// <returns>Always <see langword="true"/>, because at least one member was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    protected override bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Append(nameof(HeaderName)).Append(" = ").Append(HeaderName);
        _ = builder.Append(", ").Append(nameof(HeaderValue)).Append(" = ").Append(RedactionMarker);
        return true;
    }
}
