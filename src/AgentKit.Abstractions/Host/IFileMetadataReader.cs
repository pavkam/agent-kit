// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes metadata for already authorized file targets.</summary>
/// <remarks>The reader revalidates and consumes the grant immediately before observation.</remarks>
public interface IFileMetadataReader
{
    /// <summary>Observes metadata for one authorized target.</summary>
    /// <param name="operation">The authorized metadata read evidence.</param>
    /// <param name="cancellationToken">Propagates caller cancellation before the terminal result is returned.</param>
    /// <returns>A closed terminal metadata outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public ValueTask<FileMetadataResult> GetMetadataAsync(
        AuthorizedFileMetadataRead operation,
        CancellationToken cancellationToken = default);
}
