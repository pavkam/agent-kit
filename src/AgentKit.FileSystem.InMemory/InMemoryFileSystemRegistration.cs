// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Keyed registration for in-memory host file capabilities.</summary>
internal static class InMemoryFileSystemRegistration
{
    [Obsolete]
    internal static IServiceCollection Add(
        IServiceCollection services,
        FileSystemProfileKey key,
        Action<InMemoryFileSystemOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddAgentKitObservability();
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
        services.TryAddSingleton(TimeProvider.System);

        var optionsBuilder = services.AddOptions<InMemoryFileSystemOptions>(key.Value)
            .Validate(o => o.MaximumReadBytes > 0, "MaximumReadBytes must be positive.")
            .Validate(o => o.MaximumWriteBytes > 0, "MaximumWriteBytes must be positive.");
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        _ = services.AddKeyedSingleton(key.Value, static (provider, serviceKey) =>
        {
            var profileKey = new FileSystemProfileKey((string) serviceKey!);
            var options = Options.Create(
                provider.GetRequiredService<IOptionsMonitor<InMemoryFileSystemOptions>>().Get(profileKey.Value));
            return new InMemoryFileSystem(
                options,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<InMemoryFileSystem>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                profileKey);
        });
        _ = services.AddKeyedSingleton<IFileReader>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IFileWriter>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IFileMetadataReader>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IDirectoryCreator>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IDirectoryReader>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<ILegacyDirectoryReader>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IFileGlobber>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IFileContentSearcher>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IFileSnapshotReader>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IAtomicFileReplacer>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddKeyedSingleton<IWorkspacePatchApplier>(key.Value, static (provider, serviceKey) =>
            provider.GetRequiredKeyedService<InMemoryFileSystem>(serviceKey!));
        _ = services.AddSingleton(new FileSystemProfileRegistration(
            key,
            new FileSystemCapabilities(
                FileSystemCapability.Read
                | FileSystemCapability.Write
                | FileSystemCapability.Metadata
                | FileSystemCapability.CreateDirectory
                | FileSystemCapability.Enumerate)));
        services.TryAddSingleton<IFileSystemSelector>(static provider =>
            new DefaultFileSystemSelector(provider, provider.GetServices<FileSystemProfileRegistration>()));

        return services;
    }
}
