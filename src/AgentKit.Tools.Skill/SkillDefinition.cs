// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Maps public skill metadata to one protected, optionally integrity-pinned UTF-8 instruction file.</summary>
public sealed record SkillDefinition
{
    /// <summary>Initializes one captured skill definition.</summary>
    /// <param name="id">The stable public identity.</param>
    /// <param name="name">The bounded display name.</param>
    /// <param name="description">The bounded discovery description.</param>
    /// <param name="trust">The source provenance.</param>
    /// <param name="path">The protected backing path.</param>
    /// <param name="expectedContentHash">An optional required content fingerprint.</param>
    /// <exception cref="ArgumentException">Text or path is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="trust"/> is undefined.</exception>
    public SkillDefinition(SkillId id, string name, string description, SkillTrust trust, FileSystemPath path, ContentHash? expectedContentHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfUndefined(trust);
        ArgumentException.ThrowIfNullOrWhiteSpace(path.Value, nameof(path));
        Id = id;
        Name = name;
        Description = description;
        Trust = trust;
        Path = path;
        ExpectedContentHash = expectedContentHash;
    }

    /// <summary>Gets the stable public identity.</summary>
    public SkillId Id { get; }
    /// <summary>Gets the display name.</summary>
    public string Name { get; }
    /// <summary>Gets the discovery description.</summary>
    public string Description { get; }
    /// <summary>Gets the source provenance.</summary>
    public SkillTrust Trust { get; }
    /// <summary>Gets the protected backing path.</summary>
    public FileSystemPath Path { get; }
    /// <summary>Gets the required content hash, when pinned.</summary>
    public ContentHash? ExpectedContentHash { get; }
}
