// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> that always resolves to one
/// static, long-lived API key, shared by any provider integration package
/// that authenticates with a plain API key regardless of wire protocol.
/// </summary>
/// <remarks>
/// This is the simplest and most common provider authentication mode: the
/// application configures a single API key (typically from a secret
/// store), and every request uses it unchanged for the lifetime of this
/// instance. An application that needs to rotate keys without restarting
/// the process, or that authenticates with a refreshable OAuth token
/// instead, registers <see cref="DelegatingOAuthCredentialSource"/> in
/// place of this type rather than trying to make this type do both.
/// </remarks>
public sealed class StaticApiKeyCredentialSource: IProviderCredentialSource
{
    private readonly ApiKeyProviderCredential _credential;

    /// <summary>Initializes a new instance of the <see cref="StaticApiKeyCredentialSource"/> class.</summary>
    /// <param name="apiKey">The non-empty API key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="apiKey"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public StaticApiKeyCredentialSource(string apiKey) => _credential = new ApiKeyProviderCredential(apiKey);

    /// <inheritdoc/>
    public ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ProviderCredential>(_credential);
}
