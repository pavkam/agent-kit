// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a directory reader capability was selected for one profile key.</summary>
public sealed record FileSystemDirectoryReaderSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful directory reader selection.</summary>
    /// <param name="key">The profile key whose directory reader was selected.</param>
    /// <param name="directoryReader">The non-null directory reader instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemDirectoryReaderSelected(
        FileSystemProfileKey key,
        IDirectoryReader directoryReader,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(directoryReader);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        DirectoryReader = directoryReader;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected directory reader.</summary>
    public IDirectoryReader DirectoryReader { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
