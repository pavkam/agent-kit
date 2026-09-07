// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the sandboxed file system.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="SandboxedFileSystem"/> as the singular
        /// <see cref="IFileSystem"/>, rooted at <paramref name="rootDirectory"/>.
        /// </summary>
        /// <param name="rootDirectory">The absolute root directory every file operation is confined to.</param>
        /// <param name="configure">Optional additional configuration for <see cref="SandboxedFileSystemOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// The <see cref="IFileSystem"/> service registration itself uses
        /// <c>TryAdd</c> semantics, so calling this more than once keeps
        /// the first-registered implementation type. Its options
        /// configuration follows ordinary
        /// <see cref="Microsoft.Extensions.Options"/> composition instead:
        /// each call adds another <c>Configure</c> delegate, so calling
        /// this more than once applies <paramref name="rootDirectory"/>
        /// from the last call.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="rootDirectory"/> is null, empty, or consists
        /// only of whitespace.
        /// </exception>
        public IServiceCollection AddSandboxedFileSystem(
            string rootDirectory, Action<SandboxedFileSystemOptions>? configure = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

            _ = services.AddAgentKitObservability();

            var optionsBuilder = services.AddOptions<SandboxedFileSystemOptions>()
                .Configure(o => o.RootDirectory = rootDirectory)
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

            services.TryAddSingleton<IFileSystem, SandboxedFileSystem>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IDirectoryReader>(static provider =>
                (SandboxedFileSystem) provider.GetRequiredService<IFileSystem>());
            services.TryAddSingleton<IFileGlobber>(static provider =>
                (SandboxedFileSystem) provider.GetRequiredService<IFileSystem>());
            services.TryAddSingleton<IFileContentSearcher>(static provider =>
                (SandboxedFileSystem) provider.GetRequiredService<IFileSystem>());
            services.TryAddSingleton<IFileSnapshotReader>(static provider =>
                (SandboxedFileSystem) provider.GetRequiredService<IFileSystem>());
            services.TryAddSingleton<IAtomicFileReplacer>(static provider =>
                (SandboxedFileSystem) provider.GetRequiredService<IFileSystem>());
            services.TryAddSingleton<IWorkspacePatchApplier>(static provider =>
                (SandboxedFileSystem) provider.GetRequiredService<IFileSystem>());
            return services;
        }
    }
}
