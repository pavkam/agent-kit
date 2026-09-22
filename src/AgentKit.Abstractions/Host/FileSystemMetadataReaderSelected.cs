// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a metadata reader capability was selected for one profile key.</summary>
public sealed record FileSystemMetadataReaderSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful metadata reader selection.</summary>
    /// <param name="key">The profile key whose metadata reader was selected.</param>
    /// <param name="metadataReader">The non-null metadata reader instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemMetadataReaderSelected(
        FileSystemProfileKey key,
        IFileMetadataReader metadataReader,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(metadataReader);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        MetadataReader = metadataReader;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected metadata reader.</summary>
    public IFileMetadataReader MetadataReader { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
