// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> that delegates to an
/// application-supplied <see cref="IOAuthAccessTokenProvider"/> for OAuth
/// bearer-token authentication, shared by any provider integration package
/// that supports OAuth regardless of wire protocol.
/// </summary>
public sealed class DelegatingOAuthCredentialSource: IProviderCredentialSource
{
    private readonly IOAuthAccessTokenProvider _tokenProvider;

    /// <summary>Initializes a new instance of the <see cref="DelegatingOAuthCredentialSource"/> class.</summary>
    /// <param name="tokenProvider">Supplies the application's current OAuth access token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenProvider"/> is null.</exception>
    public DelegatingOAuthCredentialSource(IOAuthAccessTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// The configured <see cref="IOAuthAccessTokenProvider"/> returned <see langword="null"/> despite its
    /// non-nullable contract.
    /// </exception>
    public async ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return credential
            ?? throw new InvalidOperationException(
                $"{_tokenProvider.GetType()} returned a null credential from GetAccessTokenAsync, violating the non-nullable {nameof(IOAuthAccessTokenProvider)} contract.");
    }
}
