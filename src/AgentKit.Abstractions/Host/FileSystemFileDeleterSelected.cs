// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a file deleter capability was selected for one profile key.</summary>
public sealed record FileSystemFileDeleterSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful file deleter selection.</summary>
    /// <param name="key">The profile key whose deleter was selected.</param>
    /// <param name="deleter">The non-null deleter instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemFileDeleterSelected(
        FileSystemProfileKey key,
        IFileDeleter deleter,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(deleter);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        Deleter = deleter;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected deleter.</summary>
    public IFileDeleter Deleter { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
