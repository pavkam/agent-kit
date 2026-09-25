// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>The default <see cref="IProviderProfileRuntimeLease"/> implementation.</summary>
internal sealed class ProviderProfileRuntimeLease(
    ProviderEndpointProfileSnapshot endpoint,
    ProviderCredentialProfileSnapshot credential,
    IProviderCredentialSource credentialSource): IProviderProfileRuntimeLease
{
    /// <inheritdoc/>
    public ProviderEndpointProfileSnapshot Endpoint { get; } =
        endpoint ?? throw new ArgumentNullException(nameof(endpoint));

    /// <inheritdoc/>
    public ProviderCredentialProfileSnapshot Credential { get; } =
        credential ?? throw new ArgumentNullException(nameof(credential));

    /// <inheritdoc/>
    public IProviderCredentialSource CredentialSource { get; } =
        credentialSource ?? throw new ArgumentNullException(nameof(credentialSource));

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
