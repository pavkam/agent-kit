// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Tests.Fakes;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> test double that always throws
/// a scripted exception, simulating a user-supplied credential source (e.g.
/// an Entra/Azure.Identity token provider) failing during resolution.
/// </summary>
internal sealed class ThrowingProviderCredentialSource: IProviderCredentialSource
{
    private readonly Exception _exception;

    /// <summary>Initializes a new instance of the <see cref="ThrowingProviderCredentialSource"/> class.</summary>
    /// <param name="exception">The exception every call throws.</param>
    public ThrowingProviderCredentialSource(Exception exception) => _exception = exception;

    /// <inheritdoc/>
    public ValueTask<ProviderCredential> GetCredentialAsync(
        ProviderId providerId,
        CancellationToken cancellationToken = default) =>
        throw _exception;
}
