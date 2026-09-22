// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Opens bounded read handles for already authorized file targets.</summary>
/// <remarks>
/// The reader revalidates and consumes the grant immediately before observation.
/// Cancellation of <see cref="OpenReadAsync"/> prevents returning a new handle; it does not dispose an already returned handle.
/// </remarks>
public interface IFileReader
{
    /// <summary>Opens one bounded read handle for an authorized operation.</summary>
    /// <param name="operation">The authorized read evidence.</param>
    /// <param name="cancellationToken">Propagates caller cancellation before a handle is returned.</param>
    /// <returns>A handle the caller must dispose.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled before a handle is returned.</exception>
    public ValueTask<IFileReadHandle> OpenReadAsync(
        AuthorizedFileRead operation,
        CancellationToken cancellationToken = default);
}
