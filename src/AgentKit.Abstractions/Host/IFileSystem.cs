// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Reads and writes text files within a boundary a concrete implementation
/// enforces on every call.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the fuller host-access
/// boundary described by the architecture, which additionally integrates a
/// <c>SecurityGrant</c> issued by a general security authority, structured
/// directory listing, byte-level (non-text) content, and audit recording.
/// Until that exists, a caller obtains authorization separately (for
/// example, through <see cref="IToolAuthorizer"/>) and this contract only
/// re-enforces the structural sandbox boundary — never a substitute for
/// that authorization decision, only a second, independent check that a
/// higher-level allow cannot be used to reach a path outside the
/// implementation's configured root.
/// </para>
/// <para>
/// Implementations must be safe to call concurrently for independent
/// requests and must re-resolve and re-validate every path on every call;
/// they never trust a path a caller claims was already validated
/// elsewhere.
/// </para>
/// </remarks>
public interface IFileSystem
{
    /// <summary>Reads one file.</summary>
    /// <param name="request">The read request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Writes one file.</summary>
    /// <param name="request">The write request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default);
}
