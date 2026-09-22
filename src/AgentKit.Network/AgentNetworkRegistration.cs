// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Keyed dependency-injection registration for the real network leaf.</summary>
internal static class AgentNetworkRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        NetworkProfileKey key,
        Action<AgentNetworkOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new AgentNetworkOptions();
        configure?.Invoke(options);
        ValidateOptions(options);
        var snapshot = new AgentNetworkOptionsSnapshot(
            key,
            new NetworkProfileVersion(1),
            options.DestinationPolicy,
            options.AddressResolutionLifetime,
            options.MaximumResponseHeaderKilobytes,
            options.Proxy,
            options.TlsPolicy,
            options.DecompressionPolicy);
        services.TryAddKeyedSingleton(key.Value, snapshot);
        services.TryAddKeyedSingleton<INetworkNameResolver>(key.Value, static (provider, serviceKey) =>
            CreateResolver(provider, serviceKey!));
        services.TryAddKeyedSingleton<INetworkTransport>(key.Value, static (provider, serviceKey) =>
            CreateTransport(provider, serviceKey!));
        _ = services.AddSingleton(new NetworkProfileRegistration(key));
        services.TryAddSingleton<INetworkProfileSelector>(static provider =>
            new DefaultNetworkProfileSelector(provider, provider.GetServices<NetworkProfileRegistration>()));
        return services;
    }

    private static void ValidateOptions(AgentNetworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.DestinationPolicy);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.AddressResolutionLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumResponseHeaderKilobytes);
        ArgumentNullException.ThrowIfNull(options.Proxy);
    }

    private static DefaultNetworkNameResolver CreateResolver(IServiceProvider provider, object serviceKey)
    {
        var snapshot = provider.GetRequiredKeyedService<AgentNetworkOptionsSnapshot>(serviceKey);
        return new DefaultNetworkNameResolver(
            snapshot,
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetService<ILogger<DefaultNetworkNameResolver>>());
    }

    private static DefaultNetworkTransport CreateTransport(IServiceProvider provider, object serviceKey)
    {
        var snapshot = provider.GetRequiredKeyedService<AgentNetworkOptionsSnapshot>(serviceKey);
        return new DefaultNetworkTransport(
            snapshot,
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetService<ILogger<DefaultNetworkTransport>>());
    }
}
