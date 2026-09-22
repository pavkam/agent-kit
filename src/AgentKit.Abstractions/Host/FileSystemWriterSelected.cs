// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a file writer capability was selected for one profile key.</summary>
public sealed record FileSystemWriterSelected: FileSystemSelectionResult
{
    /// <summary>Initializes a successful writer selection.</summary>
    /// <param name="key">The profile key whose writer was selected.</param>
    /// <param name="writer">The non-null writer instance.</param>
    /// <param name="capabilities">The declared capabilities for the profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public FileSystemWriterSelected(
        FileSystemProfileKey key,
        IFileWriter writer,
        FileSystemCapabilities capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(capabilities);
        Key = key;
        Writer = writer;
        Capabilities = capabilities;
    }

    /// <summary>Gets the selected profile key.</summary>
    public FileSystemProfileKey Key { get; init; }

    /// <summary>Gets the selected writer.</summary>
    public IFileWriter Writer { get; init; }

    /// <summary>Gets the declared profile capabilities.</summary>
    public FileSystemCapabilities Capabilities { get; init; }
}
