// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Removes regular files for already authorized deletions.</summary>
/// <remarks>
/// <para>
/// Deletion is a separate narrow capability from <see cref="IFileWriter"/>: a writer never removes a file, and a profile that
/// declares <see cref="FileSystemCapability.Write"/> does not thereby support deletion. The deleter revalidates and atomically
/// consumes the grant immediately before mutation, emits required audit first, removes exactly the one authorized regular file
/// without following a symbolic link or leaving the configured root, and never removes a directory.
/// </para>
/// </remarks>
public interface IFileDeleter
{
    /// <summary>Gets the security audience that must appear on grants this deleter consumes.</summary>
    /// <value>A nondefault component identity matching the host boundary enforcement surface.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Deletes one authorized regular file.</summary>
    /// <param name="operation">The authorized deletion evidence.</param>
    /// <param name="cancellationToken">Propagates caller cancellation before the terminal result is returned.</param>
    /// <returns>A closed terminal deletion outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public ValueTask<FileDeleteResult> DeleteAsync(
        AuthorizedFileDelete operation,
        CancellationToken cancellationToken = default);
}
