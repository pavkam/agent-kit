// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Streams directory entries for one authorized enumeration operation.</summary>
/// <remarks>Cancellation stops observation and releases enumerator resources owned by the implementation.</remarks>
public interface IDirectoryReader
{
    /// <summary>Enumerates entries under the authorized directory target.</summary>
    /// <param name="operation">The authorized enumeration evidence.</param>
    /// <param name="cancellationToken">Stops observation and releases resources when canceled.</param>
    /// <returns>An async sequence of observed entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        CancellationToken cancellationToken = default);
}
