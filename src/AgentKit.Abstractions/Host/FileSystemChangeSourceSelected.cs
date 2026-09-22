// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a change source capability was selected for one profile key.</summary>
public sealed record FileSystemChangeSourceSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful change source selection.</summary>
    /// <param name="key">The profile key whose change source was selected.</param>
    /// <param name="changeSource">The non-null change source instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemChangeSourceSelected(
        FileSystemProfileKey key,
        IFileChangeSource changeSource,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(changeSource);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        ChangeSource = changeSource;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected change source.</summary>
    public IFileChangeSource ChangeSource { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
