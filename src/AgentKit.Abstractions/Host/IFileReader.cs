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
    /// <returns>A closed open outcome; the caller must dispose a returned handle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is null.</exception>
    public ValueTask<FileReadOpenResult> OpenReadAsync(
        AuthorizedFileRead operation,
        CancellationToken cancellationToken = default);
}
