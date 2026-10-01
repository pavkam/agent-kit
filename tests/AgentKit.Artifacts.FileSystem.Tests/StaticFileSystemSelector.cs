// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

/// <summary>Selects one fixed reader and writer for one profile key, and reports every other profile or capability as unavailable.</summary>
internal sealed class StaticFileSystemSelector(FileSystemProfileKey key, IFileReader reader, IFileWriter writer): IFileSystemSelector
{
    private static readonly FileSystemCapabilities _capabilities = new(FileSystemCapability.Read | FileSystemCapability.Write);

    /// <summary>Gets or sets whether the writer capability is reported unavailable.</summary>
    internal bool WriterUnavailable { get; set; }

    /// <summary>Gets or sets whether the reader capability is reported unavailable.</summary>
    internal bool ReaderUnavailable { get; set; }

    /// <inheritdoc/>
    public ValueTask<FileSystemSelectionResult> SelectAsync(
        FileSystemProfileKey requested,
        FileSystemCapability requiredCapability,
        CancellationToken cancellationToken = default)
    {
        FileSystemSelectionResult result = requested != key
            ? new FileSystemProfileMissing(requested)
            : requiredCapability switch
            {
                FileSystemCapability.Read when !ReaderUnavailable => new FileSystemReaderSelected(key, reader, _capabilities),
                FileSystemCapability.Write when !WriterUnavailable => new FileSystemWriterSelected(key, writer, _capabilities),
                FileSystemCapability.None or FileSystemCapability.Read or FileSystemCapability.Write or FileSystemCapability.Metadata
                    or FileSystemCapability.CreateDirectory or FileSystemCapability.Enumerate or FileSystemCapability.Watch
                    or FileSystemCapability.Temporary => new FileSystemCapabilityUnsupported(key, requiredCapability, _capabilities),
                _ => new FileSystemCapabilityUnsupported(key, requiredCapability, _capabilities),
            };
        return ValueTask.FromResult(result);
    }
}
