// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a bounded read handle was opened successfully.</summary>
public sealed record FileReadHandleOpened: FileReadOpenResult
{
    /// <summary>Initializes a new instance of the <see cref="FileReadHandleOpened"/> record.</summary>
    /// <param name="handle">The caller-owned read handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/> is null.</exception>
    public FileReadHandleOpened(IFileReadHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        Handle = handle;
    }

    /// <summary>Gets the caller-owned read handle.</summary>
    public IFileReadHandle Handle { get; init; }
}
