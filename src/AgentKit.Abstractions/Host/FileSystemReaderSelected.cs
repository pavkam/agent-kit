// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a file reader capability was selected for one profile key.</summary>
public sealed record FileSystemReaderSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful reader selection.</summary>
    /// <param name="key">The profile key whose reader was selected.</param>
    /// <param name="reader">The non-null reader instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemReaderSelected(
        FileSystemProfileKey key,
        IFileReader reader,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        Reader = reader;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected reader.</summary>
    public IFileReader Reader { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
