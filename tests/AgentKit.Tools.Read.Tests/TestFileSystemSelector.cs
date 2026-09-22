// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

/// <summary>Selects a single injected reader for tool tests.</summary>
internal sealed class TestFileSystemSelector(IFileReader reader): IFileSystemSelector
{
    private static readonly FileSystemCapabilities _capabilities = new(FileSystemCapability.Read);

    /// <inheritdoc/>
    public ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return requiredCapability == FileSystemCapability.Read
            ? ValueTask.FromResult<FileSystemSelectionResult>(new FileSystemReaderSelected(key, reader, _capabilities))
            : ValueTask.FromResult<FileSystemSelectionResult>(new FileSystemCapabilityUnsupported(key, requiredCapability, _capabilities));
    }
}
