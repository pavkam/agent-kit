// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAI;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> that always resolves to one
/// static, long-lived OpenAI API key.
/// </summary>
/// <remarks>
/// This is the simplest and most common OpenAI authentication mode: the
/// application configures a single API key (typically from a secret
/// store), and every request uses it unchanged for the lifetime of this
/// instance. Applications that need to rotate keys without restarting the
/// process, or that authenticate with a refreshable OAuth token instead,
/// register <see cref="OpenAIOAuthTokenCredentialSource"/> in place of this
/// type rather than trying to make this type do both.
/// </remarks>
public sealed class OpenAIApiKeyCredentialSource: IProviderCredentialSource
{
    private readonly ApiKeyProviderCredential _credential;

    /// <summary>Initializes a new instance of the <see cref="OpenAIApiKeyCredentialSource"/> class.</summary>
    /// <param name="apiKey">The non-empty OpenAI API key.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="apiKey"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public OpenAIApiKeyCredentialSource(string apiKey) => _credential = new ApiKeyProviderCredential(apiKey);

    /// <inheritdoc/>
    public ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ProviderCredential>(_credential);
}
