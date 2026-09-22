// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Dependency-injection registration for the in-memory file system.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="InMemoryFileSystem"/> as the singular
        /// <see cref="IFileSystem"/>, backed by a process-local virtual tree.
        /// </summary>
        /// <param name="configure">Optional additional configuration for <see cref="InMemoryFileSystemOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// The default concrete implementation is registered once and shared by every untouched filesystem capability.
        /// The <see cref="IFileSystem"/> facade and each narrow capability use independent <c>TryAdd</c> registrations,
        /// so a host may replace any one contract without changing the remaining defaults.
        /// </remarks>
        [Obsolete("Use narrow host capability contracts selected through IFileSystemSelector instead.")]
        public IServiceCollection AddInMemoryFileSystem(Action<InMemoryFileSystemOptions>? configure = null)
        {
            _ = services.AddAgentKitObservability();

            var optionsBuilder = services.AddOptions<InMemoryFileSystemOptions>()
                .Validate(o => o.MaximumReadBytes > 0, "MaximumReadBytes must be positive.")
                .Validate(o => o.MaximumWriteBytes > 0, "MaximumWriteBytes must be positive.")
                .Validate(o => o.MaximumDirectorySnapshotEntries > 0, "MaximumDirectorySnapshotEntries must be positive.")
                .Validate(o => o.MaximumSearchDepth > 0, "MaximumSearchDepth must be positive.")
                .Validate(o => o.MaximumSearchFiles > 0, "MaximumSearchFiles must be positive.")
                .Validate(o => o.MaximumSearchBytes > 0, "MaximumSearchBytes must be positive.")
                .Validate(o => o.MaximumSearchMatches > 0, "MaximumSearchMatches must be positive.")
                .Validate(o => o.MaximumSearchLineBytes > 0, "MaximumSearchLineBytes must be positive.")
                .Validate(o => o.MaximumSearchDuration > TimeSpan.Zero, "MaximumSearchDuration must be positive.")
                .Validate(o => o.MaximumPatchEntries > 0, "MaximumPatchEntries must be positive.")
                .Validate(o => o.MaximumPatchBytes > 0, "MaximumPatchBytes must be positive.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton<InMemoryFileSystem>(static provider => new(
                provider.GetRequiredService<IOptions<InMemoryFileSystemOptions>>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<InMemoryFileSystem>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>()));
            services.TryAddSingleton<IFileSystem>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ILegacyDirectoryReader>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            services.TryAddSingleton<IFileGlobber>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            services.TryAddSingleton<IFileContentSearcher>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            services.TryAddSingleton<IFileSnapshotReader>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            services.TryAddSingleton<IAtomicFileReplacer>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            services.TryAddSingleton<IWorkspacePatchApplier>(static provider => provider.GetRequiredService<InMemoryFileSystem>());
            return services;
        }

        /// <summary>Registers keyed in-memory host capabilities under <paramref name="key"/>.</summary>
        /// <param name="key">The profile key for the virtual volume.</param>
        /// <param name="configure">Optional configuration for the profile options.</param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddInMemoryFileSystem(
            FileSystemProfileKey key,
            Action<InMemoryFileSystemOptions>? configure = null) =>
            InMemoryFileSystemRegistration.Add(services, key, configure);
    }
}
