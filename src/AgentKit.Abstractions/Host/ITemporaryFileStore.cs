// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates bounded temporary files for already authorized creation operations.</summary>
/// <remarks>The store revalidates and consumes the grant immediately before creating the lease.</remarks>
public interface ITemporaryFileStore
{
    /// <summary>Creates one bounded temporary file lease.</summary>
    /// <param name="operation">The authorized temporary file creation evidence.</param>
    /// <param name="cancellationToken">Propagates caller cancellation before a lease is returned.</param>
    /// <returns>A lease the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public ValueTask<ITemporaryFileLease> CreateAsync(
        AuthorizedTemporaryFileCreation operation,
        CancellationToken cancellationToken = default);
}
