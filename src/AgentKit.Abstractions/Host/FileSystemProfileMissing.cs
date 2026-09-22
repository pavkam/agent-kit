// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no file-system profile is registered for the requested key.</summary>
public sealed record FileSystemProfileMissing: FileSystemSelectionResult
{
    /// <summary>Initializes a missing-profile outcome.</summary>
    /// <param name="key">The profile key that could not be resolved.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is default.</exception>
    public FileSystemProfileMissing(FileSystemProfileKey key)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        Key = key;
    }

    /// <summary>Gets the unresolved profile key.</summary>
    public FileSystemProfileKey Key { get; init; }
}
