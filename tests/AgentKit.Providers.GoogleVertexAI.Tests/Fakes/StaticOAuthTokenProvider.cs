// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Tests.Fakes;

/// <summary>
/// An <see cref="IOAuthAccessTokenProvider"/> test double that always
/// resolves to one fixed <see cref="OAuthTokenProviderCredential"/>.
/// </summary>
internal sealed class StaticOAuthTokenProvider: IOAuthAccessTokenProvider
{
    private readonly OAuthTokenProviderCredential _credential;

    /// <summary>Initializes a new instance of the <see cref="StaticOAuthTokenProvider"/> class.</summary>
    /// <param name="credential">The token credential every call resolves to.</param>
    public StaticOAuthTokenProvider(OAuthTokenProviderCredential credential) => _credential = credential;

    /// <inheritdoc/>
    public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_credential);
}
