// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Collections.Frozen;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Selects keyed network profiles from explicit DI registrations.</summary>
public sealed class DefaultNetworkProfileSelector: INetworkProfileSelector
{
    private readonly IServiceProvider _provider;
    private readonly FrozenSet<NetworkProfileKey> _keys;

    /// <summary>Initializes the selector from captured profile registrations.</summary>
    /// <param name="provider">The root service provider used to resolve keyed services.</param>
    /// <param name="profiles">The registered profile declarations.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public DefaultNetworkProfileSelector(
        IServiceProvider provider,
        IEnumerable<NetworkProfileRegistration> profiles)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(profiles);
        _provider = provider;
        _keys = profiles.Select(static profile => profile.Key).ToFrozenSet();
    }

    /// <inheritdoc/>
    public ValueTask<NetworkProfileSelectionResult> SelectAsync(
        NetworkProfileKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_keys.Contains(key))
        {
            return ValueTask.FromResult<NetworkProfileSelectionResult>(new NetworkProfileMissing(key));
        }

        var serviceKey = key.Value;
        return ValueTask.FromResult<NetworkProfileSelectionResult>(new NetworkProfileSelected(
            key,
            _provider.GetRequiredKeyedService<INetworkNameResolver>(serviceKey),
            _provider.GetRequiredKeyedService<INetworkTransport>(serviceKey)));
    }
}
