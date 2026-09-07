// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one exact atomic replacement conditional on the current full-content fingerprint.</summary>
public sealed record AtomicFileReplaceRequest
{
    /// <summary>Initializes an atomic replacement request.</summary>
    /// <param name="id">The mutation identity used for private staging correlation.</param>
    /// <param name="path">The existing target path.</param>
    /// <param name="expectedContentFingerprint">The required current full-content fingerprint.</param>
    /// <param name="content">The exact final bytes.</param>
    /// <param name="grant">The exact replacement authority.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public AtomicFileReplaceRequest(
        WorkspaceMutationId id,
        FileSystemPath path,
        ContentHash expectedContentFingerprint,
        ImmutableArray<byte> content,
        SecurityGrant grant)
    {
        ArgumentException.ThrowIfDefault(content);
        ArgumentNullException.ThrowIfNull(grant);
        Id = id;
        Path = path;
        ExpectedContentFingerprint = expectedContentFingerprint;
        Content = content;
        Grant = grant;
    }

    /// <summary>Gets the mutation identity.</summary>
    public WorkspaceMutationId Id { get; init; }
    /// <summary>Gets the existing target path.</summary>
    public FileSystemPath Path { get; init; }
    /// <summary>Gets the required current fingerprint.</summary>
    public ContentHash ExpectedContentFingerprint { get; init; }
    /// <summary>Gets the exact final bytes.</summary>
    public ImmutableArray<byte> Content { get; init; }
    /// <summary>Gets the exact replacement authority.</summary>
    public SecurityGrant Grant { get; init; }
}
