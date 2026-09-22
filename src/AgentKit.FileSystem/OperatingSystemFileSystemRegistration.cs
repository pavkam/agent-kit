// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Keyed dependency-injection registration for operating-system file capabilities.</summary>
internal static class OperatingSystemFileSystemRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        FileSystemProfileKey key,
        Action<OperatingSystemFileSystemOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        _ = services.AddAgentKitObservability();
        services.TryAddSingleton<IFilePathNormalizer, DefaultFilePathNormalizer>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
        services.TryAddSingleton(TimeProvider.System);

        var options = new OperatingSystemFileSystemOptions();
        configure(options);
        if (options.Roots.Count == 0)
        {
            throw new InvalidOperationException("OperatingSystemFileSystemOptions must register at least one root.");
        }

        var snapshot = new OperatingSystemFileSystemOptionsSnapshot(
            key,
            options.ProfileVersion,
            [.. options.Roots],
            options.PathPolicy,
            options.Bounds,
            options.WritePolicy,
            options.WatchPolicy);

        _ = services.AddKeyedSingleton(key.Value, snapshot);
        _ = services.AddKeyedSingleton<IFileReader>(key.Value, static (provider, serviceKey) =>
            CreateReader(provider, serviceKey!));
        _ = services.AddKeyedSingleton<IFileWriter>(key.Value, static (provider, serviceKey) =>
            CreateWriter(provider, serviceKey!));
        _ = services.AddSingleton(new FileSystemProfileRegistration(
            key,
            new FileSystemCapabilities(
                FileSystemCapability.Read | FileSystemCapability.Write | FileSystemCapability.Metadata)));
        services.TryAddSingleton<IFileSystemSelector>(static provider =>
            new DefaultFileSystemSelector(provider, provider.GetServices<FileSystemProfileRegistration>()));

        return services;
    }

    private static OperatingSystemFileReader CreateReader(IServiceProvider provider, object serviceKey)
    {
        var profileKey = new FileSystemProfileKey((string) serviceKey);
        var profile = provider.GetRequiredKeyedService<OperatingSystemFileSystemOptionsSnapshot>(serviceKey);
        return profile.ProfileKey != profileKey
            ? throw new InvalidOperationException("The keyed file-system snapshot does not match the registration key.")
            : new OperatingSystemFileReader(
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetRequiredService<TimeProvider>(),
            profile);
    }

    private static OperatingSystemFileWriter CreateWriter(IServiceProvider provider, object serviceKey)
    {
        var profileKey = new FileSystemProfileKey((string) serviceKey);
        var profile = provider.GetRequiredKeyedService<OperatingSystemFileSystemOptionsSnapshot>(serviceKey);
        return profile.ProfileKey != profileKey
            ? throw new InvalidOperationException("The keyed file-system snapshot does not match the registration key.")
            : new OperatingSystemFileWriter(
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetRequiredService<TimeProvider>(),
            profile);
    }
}
