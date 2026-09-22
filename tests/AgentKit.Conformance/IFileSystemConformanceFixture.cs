// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates an isolated file-system host profile for reusable conformance cases.</summary>
public interface IFileSystemConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets whether the implementation rejects symlink escapes when declared.</summary>
    public bool SupportsSymlinkRejection { get; }

    /// <summary>Gets the reader under test.</summary>
    public IFileReader Reader { get; }

    /// <summary>Gets the writer under test.</summary>
    public IFileWriter Writer { get; }

    /// <summary>Gets the directory creator under test when the profile exposes directory creation.</summary>
    public IDirectoryCreator? DirectoryCreator { get; }

    /// <summary>Gets the grant store used by the profile.</summary>
    public ISecurityGrantStore GrantStore { get; }

    /// <summary>Seeds a regular file with the supplied relative path and bytes.</summary>
    /// <param name="relativePath">The profile-relative path.</param>
    /// <param name="content">The file bytes.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    public ValueTask SeedFileAsync(
        string relativePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    /// <summary>Builds one authorized bounded read for a seeded file.</summary>
    /// <param name="relativePath">The profile-relative path.</param>
    /// <param name="maxBytes">The authorized byte bound.</param>
    /// <returns>The authorized read operation.</returns>
    public AuthorizedFileRead CreateAuthorizedRead(string relativePath, long maxBytes);

    /// <summary>Builds one authorized write for the supplied disposition.</summary>
    /// <param name="relativePath">The profile-relative path.</param>
    /// <param name="payload">The payload bytes.</param>
    /// <param name="disposition">The write disposition.</param>
    /// <param name="expectedTargetFingerprint">The optional target fingerprint precondition.</param>
    /// <returns>The authorized write operation.</returns>
    public AuthorizedFileWrite CreateAuthorizedWrite(
        string relativePath,
        ReadOnlyMemory<byte> payload,
        FileWriteDisposition disposition,
        ContentHash? expectedTargetFingerprint = null);

    /// <summary>Builds one authorized directory create.</summary>
    /// <param name="relativePath">The directory path relative to the profile root.</param>
    /// <returns>The authorized directory create operation.</returns>
    public AuthorizedDirectoryCreate CreateAuthorizedDirectoryCreate(string relativePath);

    /// <summary>Replaces the audit dispatcher with one that always fails required delivery.</summary>
    public void UseRejectingAudit();
}
