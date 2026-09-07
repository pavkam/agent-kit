// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates one exact new file from staged bytes.</summary>
public sealed record WorkspacePatchCreate: WorkspacePatchEntry
{
    /// <summary>Initializes an exact create entry.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="path">The target that must be absent.</param>
    /// <param name="content">The exact final bytes.</param>
    /// <param name="grant">The create authority over target and staging resources.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public WorkspacePatchCreate(
        WorkspaceMutationId id,
        FileSystemPath path,
        ImmutableArray<byte> content,
        SecurityGrant grant) : base(id, WorkspacePatchEntryKind.Create, grant)
    {
        ArgumentException.ThrowIfDefault(content);
        Path = path;
        Content = content;
    }

    /// <summary>Gets the absent target path.</summary>
    public FileSystemPath Path { get; init; }
    /// <summary>Gets the exact final bytes.</summary>
    public ImmutableArray<byte> Content { get; init; }
}
