// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

/// <summary>Selects a single injected writer for tool tests.</summary>
internal sealed class TestFileSystemSelector(IFileWriter writer): IFileSystemSelector
{
    private static readonly FileSystemCapabilities _capabilities = new(FileSystemCapability.Write);

    /// <inheritdoc/>
    public ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return requiredCapability == FileSystemCapability.Write
            ? ValueTask.FromResult<FileSystemSelectionResult>(new FileSystemWriterSelected(key, writer, _capabilities))
            : ValueTask.FromResult<FileSystemSelectionResult>(new FileSystemCapabilityUnsupported(key, requiredCapability, _capabilities));
    }
}
