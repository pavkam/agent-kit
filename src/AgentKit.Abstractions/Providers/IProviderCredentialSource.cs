// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Resolves the current <see cref="ProviderCredential"/> for one provider,
/// immediately before a concrete adapter applies it to a request.
/// </summary>
/// <remarks>
/// Implementations are registered by the concrete provider integration
/// package (for example, one that reads a static API key from options, or
/// one that delegates to an application-owned OAuth token cache) and are
/// resolved fresh for every attempt rather than cached by the caller, so a
/// rotated key or refreshed token takes effect on the next request without
/// requiring the adapter to be reconstructed. Implementations must be safe
/// to call concurrently from multiple in-flight requests.
/// </remarks>
public interface IProviderCredentialSource
{
    /// <summary>
    /// Resolves the current credential to use for a request to
    /// <paramref name="providerId"/>.
    /// </summary>
    /// <param name="providerId">The provider the resolved credential authenticates against.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The current provider credential.</returns>
    public ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default);
}
