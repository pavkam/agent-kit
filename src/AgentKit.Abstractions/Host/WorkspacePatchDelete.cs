// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Deletes one exact existing file version.</summary>
public sealed record WorkspacePatchDelete: WorkspacePatchEntry
{
    /// <summary>Initializes an exact delete entry.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="path">The existing target.</param>
    /// <param name="expectedContentFingerprint">The required current fingerprint.</param>
    /// <param name="grant">The delete authority.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public WorkspacePatchDelete(
        WorkspaceMutationId id,
        FileSystemPath path,
        ContentHash expectedContentFingerprint,
        SecurityGrant grant) : base(id, WorkspacePatchEntryKind.Delete, grant)
    {
        Path = path;
        ExpectedContentFingerprint = expectedContentFingerprint;
    }

    /// <summary>Gets the existing target.</summary>
    public FileSystemPath Path { get; init; }
    /// <summary>Gets the required current fingerprint.</summary>
    public ContentHash ExpectedContentFingerprint { get; init; }
}
