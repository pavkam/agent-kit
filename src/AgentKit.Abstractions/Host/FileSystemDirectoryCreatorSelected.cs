// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a directory creator capability was selected for one profile key.</summary>
public sealed record FileSystemDirectoryCreatorSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful directory creator selection.</summary>
    /// <param name="key">The profile key whose directory creator was selected.</param>
    /// <param name="directoryCreator">The non-null directory creator instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemDirectoryCreatorSelected(
        FileSystemProfileKey key,
        IDirectoryCreator directoryCreator,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(directoryCreator);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        DirectoryCreator = directoryCreator;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected directory creator.</summary>
    public IDirectoryCreator DirectoryCreator { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
