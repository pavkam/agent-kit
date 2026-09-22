// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one narrow file-system capability a profile may expose.</summary>
/// <remarks>Selection rejects an operation when the required capability is absent from the registered profile.</remarks>
[Flags]
public enum FileSystemCapability
{
    /// <summary>No capability.</summary>
    None = 0,

    /// <summary>Bounded byte reads through <see cref="IFileReader"/>.</summary>
    Read = 1 << 0,

    /// <summary>Explicit-disposition writes through <see cref="IFileWriter"/>.</summary>
    Write = 1 << 1,

    /// <summary>Streaming directory enumeration through spec <see cref="IDirectoryReader"/>.</summary>
    Enumerate = 1 << 2,

    /// <summary>Target metadata observation through <see cref="IFileMetadataReader"/>.</summary>
    Metadata = 1 << 3,

    /// <summary>Change observation through <see cref="IFileChangeSource"/>.</summary>
    Watch = 1 << 4,

    /// <summary>Bounded temporary file creation through <see cref="ITemporaryFileStore"/>.</summary>
    Temporary = 1 << 5,

    /// <summary>Explicit parent-directory creation through <see cref="IDirectoryCreator"/>.</summary>
    CreateDirectory = 1 << 6,
}
