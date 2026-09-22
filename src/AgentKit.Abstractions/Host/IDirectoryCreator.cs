// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates directories for already authorized creation operations.</summary>
/// <remarks>
/// Directory creation is a separate effect from file writing. The creator revalidates and consumes
/// the grant immediately before mutation.
/// </remarks>
public interface IDirectoryCreator
{
    /// <summary>Creates one authorized directory target.</summary>
    /// <param name="operation">The authorized directory creation evidence.</param>
    /// <param name="cancellationToken">Propagates caller cancellation before the terminal result is returned.</param>
    /// <returns>A closed terminal directory creation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public ValueTask<DirectoryCreateResult> CreateAsync(
        AuthorizedDirectoryCreate operation,
        CancellationToken cancellationToken = default);
}
