// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> that delegates to an
/// application-supplied <see cref="IOpenAIOAuthTokenProvider"/> for OAuth
/// bearer-token authentication.
/// </summary>
public sealed class OpenAIOAuthTokenCredentialSource: IProviderCredentialSource
{
    private readonly IOpenAIOAuthTokenProvider _tokenProvider;

    /// <summary>Initializes a new instance of the <see cref="OpenAIOAuthTokenCredentialSource"/> class.</summary>
    /// <param name="tokenProvider">Supplies the application's current OAuth access token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tokenProvider"/> is null.</exception>
    public OpenAIOAuthTokenCredentialSource(IOpenAIOAuthTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    public async ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default) =>
        await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
}
