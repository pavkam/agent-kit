// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for the in-memory file system.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers a keyed in-memory host volume under <paramref name="key"/>.</summary>
        /// <param name="key">The profile key for the virtual volume.</param>
        /// <param name="configure">Optional configuration for the profile options.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// Registers one <see cref="InMemoryFileSystem"/> per key, shared by the keyed <see cref="IFileReader"/>,
        /// <see cref="IFileWriter"/>, <see cref="IFileDeleter"/>, <see cref="IFileMetadataReader"/>, <see cref="IDirectoryCreator"/>,
        /// <see cref="IDirectoryReader"/>, <see cref="IFileGlobber"/>, <see cref="IFileContentSearcher"/>,
        /// <see cref="IFileSnapshotReader"/>, <see cref="IAtomicFileReplacer"/>, and <see cref="IWorkspacePatchApplier"/>
        /// registrations, plus the profile registration that <see cref="IFileSystemSelector"/> discovers. Options are
        /// named by the key, so each key has independent bounds; <paramref name="configure"/> adds to those options.
        /// </para>
        /// <para>
        /// Registering the same key twice adds a second keyed registration; the later registration wins keyed resolution
        /// and both appear to the selector, so give each volume a distinct key. Shared defaults for enforcement-intent
        /// and audit-record identities and the clock use <c>TryAdd</c> and never replace a host choice.
        /// </para>
        /// </remarks>
        public IServiceCollection AddInMemoryFileSystem(
            FileSystemProfileKey key,
            Action<InMemoryFileSystemOptions>? configure = null) =>
            InMemoryFileSystemRegistration.Add(services, key, configure);
    }
}
