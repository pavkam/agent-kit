// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

/// <summary>
/// Supplies the application's current OpenAI OAuth access token to
/// <see cref="OpenAIOAuthTokenCredentialSource"/>.
/// </summary>
/// <remarks>
/// AgentKit does not implement an OAuth authorization or refresh flow
/// itself. The application (or a dedicated identity package) implements
/// this interface to expose whatever token it has already obtained and
/// refreshed through its own flow; this integration only consumes the
/// resulting token and rejects it once expired.
/// </remarks>
public interface IOpenAIOAuthTokenProvider
{
    /// <summary>Gets the application's current OAuth access token.</summary>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The current OAuth access token credential.</returns>
    public ValueTask<OAuthTokenProviderCredential> GetTokenAsync(CancellationToken cancellationToken = default);
}
