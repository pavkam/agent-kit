// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for operating-system file-system profiles.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers operating-system file capabilities under <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The profile key selecting this virtual file system.</param>
        /// <param name="configure">Configures roots, bounds, and policies for the profile.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Configuration does not register any roots.</exception>
        /// <remarks>
        /// <para>
        /// Registers, under <paramref name="key"/>, the <see cref="IFileReader"/>, <see cref="IFileWriter"/>,
        /// <see cref="IDirectoryReader"/>, <see cref="IFileGlobber"/>, <see cref="IFileContentSearcher"/>,
        /// <see cref="IFileSnapshotReader"/>, <see cref="IAtomicFileReplacer"/>, and <see cref="IWorkspacePatchApplier"/>
        /// capabilities plus the profile registration that <see cref="IFileSystemSelector"/> discovers. Workspace
        /// operations observe the first registered root. The delegate runs once, during this call.
        /// </para>
        /// <para>
        /// Registering the same key twice adds a second keyed registration; the later registration wins keyed resolution
        /// and both appear to the selector, so give each profile a distinct key. Shared defaults for the path normalizer,
        /// enforcement-intent and audit-record identities, and the clock use <c>TryAdd</c> and never replace a host choice.
        /// </para>
        /// </remarks>
        public IServiceCollection AddOperatingSystemFileSystem(
            FileSystemProfileKey key,
            Action<OperatingSystemFileSystemOptions> configure) =>
            OperatingSystemFileSystemRegistration.Add(services, key, configure);
    }
}
