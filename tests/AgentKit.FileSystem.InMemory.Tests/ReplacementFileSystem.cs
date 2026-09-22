// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory.Tests;

/// <summary>Provides a distinguishable replacement for registration-order tests without performing host effects.</summary>
[Obsolete]
internal sealed class ReplacementFileSystem: IFileSystem
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("replacement-file-system");

    /// <inheritdoc/>
    [Obsolete]
    public Task<FileReadResult> ReadAsync(LegacyFileReadRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    [Obsolete]
    public Task<LegacyFileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
