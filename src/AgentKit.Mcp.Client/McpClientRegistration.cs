// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Owns MCP client dependency-injection registration and validation.</summary>
internal static class McpClientRegistration
{
    internal static IServiceCollection Add(IServiceCollection services, Action<McpClientOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        var state = GetOrCreateOptionsState(services);
        state.Configure(configure);
        _ = GetOrCreateRegistrationState(services);
        services.TryAddSingleton(static provider => provider.GetRequiredService<McpClientOptionsState>().Snapshot);
        services.TryAddSingleton<IMcpEndpointCatalog, DefaultMcpEndpointCatalog>();
        services.TryAddSingleton<IMcpCapabilityProfileCatalog, DefaultMcpCapabilityProfileCatalog>();
        services.TryAddSingleton<IMcpTransportFactoryCatalog, DefaultMcpTransportFactoryCatalog>();
        services.TryAddSingleton<IIdentifierGenerator<McpSessionId>, GuidMcpSessionIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<McpRequestId>, GuidMcpRequestIdGenerator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMcpClientSessionFactory, McpClientSessionFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMcpTransportFactory, StdioMcpTransportFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMcpTransportFactory, HttpMcpTransportFactory>());
        return services;
    }

    internal static IServiceCollection AddStdioEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        Action<McpStdioEndpointOptions> configure) =>
        RegisterStdioEndpoint(services, key, configure, replace: false);

    internal static IServiceCollection ReplaceStdioEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        Action<McpStdioEndpointOptions> configure) =>
        RegisterStdioEndpoint(services, key, configure, replace: true);

    internal static IServiceCollection AddHttpEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        Action<McpHttpEndpointOptions> configure) =>
        RegisterHttpEndpoint(services, key, configure, replace: false);

    internal static IServiceCollection ReplaceHttpEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        Action<McpHttpEndpointOptions> configure) =>
        RegisterHttpEndpoint(services, key, configure, replace: true);

    internal static IServiceCollection AddCapabilityProfile(
        IServiceCollection services,
        CapabilityProfileId profileId,
        Action<McpCapabilityProfileOptions> configure) =>
        RegisterCapabilityProfile(services, profileId, configure, replace: false);

    internal static IServiceCollection ReplaceCapabilityProfile(
        IServiceCollection services,
        CapabilityProfileId profileId,
        Action<McpCapabilityProfileOptions> configure) =>
        RegisterCapabilityProfile(services, profileId, configure, replace: true);

    internal static IServiceCollection ReplaceSessionFactory<TFactory>(IServiceCollection services)
        where TFactory : class, IMcpClientSessionFactory
    {
        ArgumentNullException.ThrowIfNull(services);
        RemoveDescriptors(services, static descriptor => descriptor.ServiceType == typeof(IMcpClientSessionFactory));
        _ = services.AddSingleton<IMcpClientSessionFactory, TFactory>();
        return services;
    }

    internal static IServiceCollection ReplaceEndpointCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IMcpEndpointCatalog
    {
        ArgumentNullException.ThrowIfNull(services);
        RemoveDescriptors(services, static descriptor => descriptor.ServiceType == typeof(IMcpEndpointCatalog));
        _ = services.AddSingleton<IMcpEndpointCatalog, TCatalog>();
        return services;
    }

    internal static IServiceCollection ReplaceCapabilityProfileCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IMcpCapabilityProfileCatalog
    {
        ArgumentNullException.ThrowIfNull(services);
        RemoveDescriptors(
            services,
            static descriptor => descriptor.ServiceType == typeof(IMcpCapabilityProfileCatalog));
        _ = services.AddSingleton<IMcpCapabilityProfileCatalog, TCatalog>();
        return services;
    }

    internal static void ValidateClientOptions(McpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.HandshakeTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.RequestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ShutdownTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumFrameBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumMessageBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumInFlightRequests);
        var maximumFrameBytes = options.MaximumFrameBytes;
        var maximumMessageBytes = options.MaximumMessageBytes;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumFrameBytes, maximumMessageBytes, nameof(maximumFrameBytes));
    }

    private static IServiceCollection RegisterStdioEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        Action<McpStdioEndpointOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureOptionsState(services);
        var options = new McpStdioEndpointOptions();
        configure(options);
        ValidateStdioOptions(options);
        var snapshot = GetOptionsSnapshot(services);
        var endpoint = new McpEndpoint(
            key,
            NextRevision(services, key, replace),
            new McpStdioTransportProfile(options.Command, [.. options.Arguments]),
            authentication: null,
            snapshot.CreateEndpointBounds());
        return RegisterEndpoint(services, key, endpoint, replace);
    }

    private static IServiceCollection RegisterHttpEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        Action<McpHttpEndpointOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        EnsureOptionsState(services);
        var options = new McpHttpEndpointOptions();
        configure(options);
        ValidateHttpOptions(options);
        var snapshot = GetOptionsSnapshot(services);
        var endpoint = new McpEndpoint(
            key,
            NextRevision(services, key, replace),
            new McpHttpTransportProfile(options.Endpoint!),
            options.Authentication,
            snapshot.CreateEndpointBounds());
        return RegisterEndpoint(services, key, endpoint, replace);
    }

    private static IServiceCollection RegisterCapabilityProfile(
        IServiceCollection services,
        CapabilityProfileId profileId,
        Action<McpCapabilityProfileOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        var options = new McpCapabilityProfileOptions();
        configure(options);
        var endpointKeys = ValidateProfileOptions(options);
        var profile = new McpCapabilityProfile(
            McpCapabilityIds.Client,
            profileId,
            NextProfileRevision(services, profileId, replace),
            endpointKeys);
        return RegisterCapabilityProfile(services, profileId, profile, replace);
    }

    private static IServiceCollection RegisterEndpoint(
        IServiceCollection services,
        McpEndpointKey key,
        McpEndpoint endpoint,
        bool replace)
    {
        var registrationState = GetOrCreateRegistrationState(services);
        if (registrationState.TryGetEndpoint(key, out var existing) && !replace)
        {
            return existing?.Equals(endpoint) == true
                ? services
                : throw new InvalidOperationException(
                    $"MCP endpoint key '{key.Value}' is already registered. Use ReplaceMcpStdioEndpoint or ReplaceMcpHttpEndpoint to change it.");
        }

        RemoveKeyedRegistrations(services, typeof(McpEndpoint), key.Value);
        _ = services.AddKeyedSingleton(key.Value, endpoint);
        registrationState.SetEndpoint(endpoint);
        RemoveRegistrationMarkers(services, key);
        _ = services.AddSingleton(new McpEndpointRegistration(key));
        return services;
    }

    private static IServiceCollection RegisterCapabilityProfile(
        IServiceCollection services,
        CapabilityProfileId profileId,
        McpCapabilityProfile profile,
        bool replace)
    {
        var registrationState = GetOrCreateRegistrationState(services);
        if (registrationState.TryGetProfile(profileId, out var existing) && !replace)
        {
            return existing?.Equals(profile) == true
                ? services
                : throw new InvalidOperationException(
                    $"MCP capability profile '{profileId.Value}' is already registered. Use ReplaceMcpCapabilityProfile to change it.");
        }

        RemoveKeyedRegistrations(services, typeof(McpCapabilityProfile), profileId.Value);
        _ = services.AddKeyedSingleton(profileId.Value, profile);
        registrationState.SetProfile(profile);
        RemoveProfileRegistrationMarkers(services, profileId);
        _ = services.AddSingleton(new McpCapabilityProfileRegistration(profileId));
        return services;
    }

    private static void ValidateStdioOptions(McpStdioEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Command, nameof(options.Command));
    }

    private static void ValidateHttpOptions(McpHttpEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Endpoint is null)
        {
            throw new InvalidOperationException("McpHttpEndpointOptions.Endpoint must be configured.");
        }

        _ = new McpHttpTransportProfile(options.Endpoint);
    }

    private static ImmutableArray<McpEndpointKey> ValidateProfileOptions(McpCapabilityProfileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.EndpointKeys.Count == 0)
        {
            return [];
        }

        var keys = ImmutableArray.CreateBuilder<McpEndpointKey>(options.EndpointKeys.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in options.EndpointKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(options.EndpointKeys));
            if (!seen.Add(key.Value))
            {
                throw new InvalidOperationException(
                    $"MCP capability profile endpoint keys must be unique. Duplicate key '{key.Value}'.");
            }

            keys.Add(key);
        }

        return keys.ToImmutable();
    }

    private static McpEndpointRevision NextRevision(IServiceCollection services, McpEndpointKey key, bool replace)
    {
        var registrationState = GetOrCreateRegistrationState(services);
        return !registrationState.TryGetEndpoint(key, out var existing) || existing is null
            ? new McpEndpointRevision(1)
            : !replace
                ? existing.Revision
                : new McpEndpointRevision(existing.Revision.Value + 1);
    }

    private static McpCapabilityProfileRevision NextProfileRevision(
        IServiceCollection services,
        CapabilityProfileId profileId,
        bool replace)
    {
        var registrationState = GetOrCreateRegistrationState(services);
        return !registrationState.TryGetProfile(profileId, out var existing) || existing is null
            ? new McpCapabilityProfileRevision(1)
            : !replace
                ? existing.Revision
                : new McpCapabilityProfileRevision(existing.Revision.Value + 1);
    }

    private static void EnsureOptionsState(IServiceCollection services) =>
        _ = GetOrCreateOptionsState(services);

    private static McpClientOptionsSnapshot GetOptionsSnapshot(IServiceCollection services) =>
        GetOrCreateOptionsState(services).Snapshot;

    private static McpClientRegistrationState GetOrCreateRegistrationState(IServiceCollection services)
    {
        foreach (var registration in services)
        {
            if (registration.ServiceType == typeof(McpClientRegistrationState)
                && registration.ImplementationInstance is McpClientRegistrationState existing)
            {
                return existing;
            }
        }

        var created = new McpClientRegistrationState();
        _ = services.AddSingleton(created);
        return created;
    }

    private static void RemoveKeyedRegistrations(IServiceCollection services, Type serviceType, string key)
    {
        foreach (var descriptor in services
            .Where(descriptor =>
                descriptor.IsKeyedService
                && descriptor.ServiceType == serviceType
                && string.Equals((string?) descriptor.ServiceKey, key, StringComparison.Ordinal))
            .ToArray())
        {
            _ = services.Remove(descriptor);
        }
    }

    private static McpClientOptionsState GetOrCreateOptionsState(IServiceCollection services)
    {
        foreach (var registration in services)
        {
            if (registration.ServiceType != typeof(McpClientOptionsState))
            {
                continue;
            }

            if (registration.ImplementationInstance is McpClientOptionsState existing)
            {
                return existing;
            }
        }

        var created = new McpClientOptionsState();
        _ = services.AddSingleton(created);
        return created;
    }

    private static void RemoveRegistrationMarkers(IServiceCollection services, McpEndpointKey key)
    {
        var markers = services
            .Where(static descriptor =>
                !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(McpEndpointRegistration))
            .ToArray();
        foreach (var marker in markers)
        {
            if (marker.ImplementationInstance is McpEndpointRegistration registration && registration.Key == key)
            {
                _ = services.Remove(marker);
            }
        }
    }

    private static void RemoveProfileRegistrationMarkers(IServiceCollection services, CapabilityProfileId profileId)
    {
        var markers = services
            .Where(static descriptor =>
                !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(McpCapabilityProfileRegistration))
            .ToArray();
        foreach (var marker in markers)
        {
            if (marker.ImplementationInstance is McpCapabilityProfileRegistration registration
                && registration.ProfileId == profileId)
            {
                _ = services.Remove(marker);
            }
        }
    }

    private static void RemoveDescriptors(
        IServiceCollection services,
        Func<ServiceDescriptor, bool> predicate)
    {
        foreach (var descriptor in services.Where(predicate).ToArray())
        {
            _ = services.Remove(descriptor);
        }
    }
}
