// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a temporary file store capability was selected for one profile key.</summary>
public sealed record FileSystemTemporaryStoreSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful temporary store selection.</summary>
    /// <param name="key">The profile key whose temporary store was selected.</param>
    /// <param name="temporaryStore">The non-null temporary store instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemTemporaryStoreSelected(
        FileSystemProfileKey key,
        ITemporaryFileStore temporaryStore,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(temporaryStore);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        TemporaryStore = temporaryStore;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected temporary store.</summary>
    public ITemporaryFileStore TemporaryStore { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
