// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A provider credential consisting of one previously obtained OAuth access
/// token, supplied by the application after it has completed whatever
/// authorization flow and, when applicable, refresh it owns.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. Equality and hashing over a secret value is intentional here
/// (values are compared, not logged), but callers must still take care
/// never to place an instance where it could be serialized, logged, or
/// otherwise made observable outside the request pipeline that consumes it.
/// </para>
/// <para>
/// AgentKit does not implement an OAuth token-acquisition or refresh flow
/// itself: the application (or a dedicated identity package) is responsible
/// for obtaining and refreshing <see cref="AccessToken"/> and supplying the
/// current value through its provider credential source implementation. A
/// concrete adapter that receives a credential whose
/// <see cref="ExpiresAtUtc"/> has already passed fails the attempt with a
/// typed <see cref="ProviderFailureKind.Authentication"/> failure before
/// sending any request, rather than sending a request it can predict will
/// be rejected.
/// </para>
/// </remarks>
public sealed record OAuthTokenProviderCredential: ProviderCredential
{
    /// <summary>Initializes a new instance of the <see cref="OAuthTokenProviderCredential"/> record.</summary>
    /// <param name="accessToken">The non-empty bearer access token text.</param>
    /// <param name="expiresAtUtc">
    /// The UTC instant at which <paramref name="accessToken"/> expires, when
    /// known; <see langword="null"/> when the token's expiry is not tracked.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="accessToken"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public OAuthTokenProviderCredential(string accessToken, DateTimeOffset? expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Gets the bearer access token text.</summary>
    public string AccessToken { get; init; }

    /// <summary>
    /// Gets the UTC instant at which <see cref="AccessToken"/> expires, when
    /// known.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
