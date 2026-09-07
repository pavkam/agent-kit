// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

/// <summary>Provides a distinguishable replacement for registration-order tests without performing host effects.</summary>
internal sealed class ReplacementFileSystem: IFileSystem
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("replacement-file-system");

    /// <inheritdoc/>
    public Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
