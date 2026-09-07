// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Tests.Fakes;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> test double that always
/// resolves to one fixed <see cref="ProviderCredential"/>, honoring
/// cancellation so tests can simulate cancellation at the credential
/// resolution stage.
/// </summary>
internal sealed class StaticProviderCredentialSource: IProviderCredentialSource
{
    private readonly ProviderCredential _credential;

    /// <summary>Initializes a new instance of the <see cref="StaticProviderCredentialSource"/> class.</summary>
    /// <param name="credential">The credential every call resolves to.</param>
    public StaticProviderCredentialSource(ProviderCredential credential) => _credential = credential;

    /// <inheritdoc/>
    public ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_credential);
    }
}
