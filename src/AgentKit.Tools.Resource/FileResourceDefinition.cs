// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

/// <summary>Maps public resource metadata to one protected workspace-relative file snapshot.</summary>
public sealed record FileResourceDefinition
{
    /// <summary>Initializes one immutable file resource definition.</summary>
    /// <param name="id">The public stable resource identity.</param>
    /// <param name="kind">The resource kind.</param>
    /// <param name="trust">The source provenance class.</param>
    /// <param name="description">A non-blank bounded host-authored description.</param>
    /// <param name="path">The protected backing file path, never listed to the model.</param>
    /// <param name="mediaType">The declared textual media type.</param>
    /// <param name="expectedContentHash">An optional required exact content fingerprint.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> or <paramref name="trust"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="description"/> or <paramref name="mediaType"/> is blank.</exception>
    public FileResourceDefinition(
        ResourceId id,
        ResourceKind kind,
        ResourceTrust trust,
        string description,
        FileSystemPath path,
        string mediaType,
        ContentHash? expectedContentHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(trust);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(path.Value, nameof(path));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        Id = id;
        Kind = kind;
        Trust = trust;
        Description = description;
        Path = path;
        MediaType = mediaType;
        ExpectedContentHash = expectedContentHash;
    }

    /// <summary>Gets the stable public identity.</summary>
    public ResourceId Id { get; }
    /// <summary>Gets the declared resource kind.</summary>
    public ResourceKind Kind { get; }
    /// <summary>Gets the source provenance class.</summary>
    public ResourceTrust Trust { get; }
    /// <summary>Gets the host-authored description.</summary>
    public string Description { get; }
    /// <summary>Gets the protected backing path.</summary>
    public FileSystemPath Path { get; }
    /// <summary>Gets the declared textual media type.</summary>
    public string MediaType { get; }
    /// <summary>Gets the required content hash, when integrity is pinned.</summary>
    public ContentHash? ExpectedContentHash { get; }
}
