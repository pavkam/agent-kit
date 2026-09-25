// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Builds immutable profile snapshots from configured options.</summary>
internal static class ProviderProfileSnapshots
{
    /// <summary>Creates an endpoint snapshot from validated options.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="options">The configured options.</param>
    /// <returns>The immutable snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="InvalidOperationException">Required option fields are missing.</exception>
    internal static ProviderEndpointProfileSnapshot CreateEndpoint(
        ProviderEndpointProfileKey key,
        ProviderEndpointProfileOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ProviderId is null
            || options.ServiceSurface is null
            || options.EndpointId is null
            || options.BaseAddress is null)
        {
            throw new InvalidOperationException("Endpoint profile options must specify provider, service surface, endpoint, and base address.");
        }

        var reference = new ProviderEndpointProfileReference(key, options.Version);
        var fingerprint = new ContentHash($"endpoint:{key}:{options.Version.Value}");
        return new ProviderEndpointProfileSnapshot(
            reference,
            options.ProviderId.Value,
            options.ServiceSurface.Value,
            options.EndpointId.Value,
            options.BaseAddress,
            options.ApiVersion,
            fingerprint,
            ExtensionData.Empty);
    }

    /// <summary>Creates a credential snapshot from validated options.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="options">The configured options.</param>
    /// <returns>The immutable snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="InvalidOperationException">Required option fields are missing.</exception>
    internal static ProviderCredentialProfileSnapshot CreateCredential(
        ProviderCredentialProfileKey key,
        ProviderCredentialProfileOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ProviderId is null || options.ServiceSurface is null || options.SourceKey is null)
        {
            throw new InvalidOperationException("Credential profile options must specify provider, service surface, and source key.");
        }

        var reference = new ProviderCredentialProfileReference(key, options.Version);
        var fingerprint = new ContentHash($"credential:{key}:{options.Version.Value}");
        return new ProviderCredentialProfileSnapshot(
            reference,
            options.ProviderId.Value,
            options.ServiceSurface.Value,
            options.SourceKey.Value,
            options.AccountId,
            options.RefreshSkew,
            fingerprint,
            ExtensionData.Empty);
    }
}
