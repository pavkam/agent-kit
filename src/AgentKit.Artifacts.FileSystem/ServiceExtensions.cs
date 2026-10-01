// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Registers artifact storage over AgentKit's protected file-system contracts.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed artifact store that writes through an explicitly selected file-system profile.</summary>
        /// <param name="key">The backend key a profile route names; repeated registration of the same key is ignored.</param>
        /// <param name="target">The file-system profile, logical root, and host root the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional bounds.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="IFileSystemSelector"/>, an <see cref="ISecurityAuthoritySelector"/>, and an <see cref="ISecurityGrantStore"/>. The profile it selects must declare the read, write, enumerate, and delete capabilities. It performs no effect until its first operation, and no persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddFileSystemArtifactStore(ArtifactBackendKey key, FileSystemArtifactTarget target, Action<FileSystemArtifactOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new FileSystemArtifactOptions();
            configure?.Invoke(options);
            var settings = new FileSystemArtifactSettings(
                options.MaximumRecordBytes, options.MaximumLogBytes, options.MaximumPayloadBytes, options.CompactionRecordThreshold,
                options.EffectAuthorizationLifetime);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<FileOperationId>, GuidFileOperationIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
            services.TryAddKeyedSingleton<IArtifactStore>(key.Value, (provider, _) => new FileSystemArtifactStore(
                target,
                settings,
                provider.GetRequiredService<IFileSystemSelector>(),
                provider.GetRequiredService<ISecurityAuthoritySelector>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityRequestId>>(),
                provider.GetRequiredService<IIdentifierGenerator<FileOperationId>>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<FileSystemArtifactStore>>()));
            return services;
        }
    }
}
