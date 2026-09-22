// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Commits already authorized file writes with explicit dispositions.</summary>
/// <remarks>
/// The writer revalidates and consumes the grant immediately before mutation and returns a closed
/// <see cref="FileWriteResult"/> for every reachable outcome.
/// </remarks>
public interface IFileWriter
{
    /// <summary>Writes one authorized payload under the declared disposition.</summary>
    /// <param name="operation">The authorized write evidence.</param>
    /// <param name="content">The bounded payload and fingerprint to commit.</param>
    /// <param name="cancellationToken">Propagates caller cancellation before the terminal result is returned.</param>
    /// <returns>A closed terminal write outcome.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public ValueTask<FileWriteResult> WriteAsync(
        AuthorizedFileWrite operation,
        FileWriteContent content,
        CancellationToken cancellationToken = default);
}
