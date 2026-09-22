// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Reads and writes text files through a security-enforcing host boundary.
/// </summary>
/// <remarks>
/// <para>
/// Every request carries a bounded <see cref="SecurityGrant"/>. The concrete
/// implementation recomputes canonical resource and input evidence and
/// atomically validates and consumes that grant immediately before any
/// existence check, metadata observation, content read, or mutation.
/// </para>
/// <para>
/// Implementations must be safe to call concurrently for independent
/// requests and must re-resolve and re-validate every path on every call;
/// they never trust a path a caller claims was already validated
/// elsewhere.
/// </para>
/// </remarks>
[Obsolete("Use IFileReader, IFileWriter, and the other narrow capability contracts selected through IFileSystemSelector instead.")]
public interface IFileSystem
{
    /// <summary>Gets the component identity to which file-operation grants must be addressed.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Reads one file.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<FileReadResult> ReadAsync(LegacyFileReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Writes one file.</summary>
    /// <param name="request">The write request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="FileWriteRequest.Mode"/> is not a defined <see cref="FileWriteMode"/> value.
    /// </exception>
    public Task<LegacyFileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default);
}
