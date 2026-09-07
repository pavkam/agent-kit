// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Replaces one exact existing file version with staged bytes.</summary>
public sealed record WorkspacePatchReplace: WorkspacePatchEntry
{
    /// <summary>Initializes an exact replace entry.</summary>
    /// <param name="id">The mutation identity.</param>
    /// <param name="path">The existing target.</param>
    /// <param name="expectedContentFingerprint">The required current fingerprint.</param>
    /// <param name="content">The exact final bytes.</param>
    /// <param name="grant">The replace authority over target and staging resources.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public WorkspacePatchReplace(
        WorkspaceMutationId id,
        FileSystemPath path,
        ContentHash expectedContentFingerprint,
        ImmutableArray<byte> content,
        SecurityGrant grant) : base(id, WorkspacePatchEntryKind.Replace, grant)
    {
        ArgumentException.ThrowIfDefault(content);
        Path = path;
        ExpectedContentFingerprint = expectedContentFingerprint;
        Content = content;
    }

    /// <summary>Gets the existing target.</summary>
    public FileSystemPath Path { get; init; }
    /// <summary>Gets the required current fingerprint.</summary>
    public ContentHash ExpectedContentFingerprint { get; init; }
    /// <summary>Gets the exact final bytes.</summary>
    public ImmutableArray<byte> Content { get; init; }
}
