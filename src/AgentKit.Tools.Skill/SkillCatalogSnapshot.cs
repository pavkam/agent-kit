// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Captures one immutable skill catalog version shared by context discovery and activation.</summary>
public sealed record SkillCatalogSnapshot
{
    /// <summary>Initializes a captured catalog.</summary>
    /// <param name="version">The deterministic metadata fingerprint.</param>
    /// <param name="skills">The ordered definitions.</param>
    /// <exception cref="ArgumentException">The version is blank, the array is invalid, or IDs repeat.</exception>
    public SkillCatalogSnapshot(string version, ImmutableArray<SkillDefinition> skills)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfContainsNull(skills);
        ArgumentException.ThrowIfDuplicateSkillIds(skills);

        Version = version;
        Skills = skills;
    }

    /// <summary>Gets the deterministic metadata fingerprint.</summary>
    public string Version { get; }
    /// <summary>Gets the ordered definitions.</summary>
    public ImmutableArray<SkillDefinition> Skills { get; }
}
