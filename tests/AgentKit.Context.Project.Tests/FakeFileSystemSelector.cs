// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project.Tests;

/// <summary>Selects one fake reader for one profile key and rejects every other profile or capability.</summary>
internal sealed class FakeFileSystemSelector(FileSystemProfileKey profileKey, IFileReader reader): IFileSystemSelector
{
    private static readonly FileSystemCapabilities _capabilities = new(FileSystemCapability.Read);

    /// <inheritdoc/>
    public ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey key,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemSelectionResult result = key != profileKey
            ? new FileSystemProfileMissing(key)
            : requiredCapability == FileSystemCapability.Read
                ? new FileSystemReaderSelected(key, reader, _capabilities)
                : new FileSystemCapabilityUnsupported(key, requiredCapability, _capabilities);
        return ValueTask.FromResult(result);
    }
}
