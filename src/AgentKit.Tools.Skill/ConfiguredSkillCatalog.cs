// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Captures one validated immutable skill catalog and renders its discovery inventory.</summary>
public sealed class ConfiguredSkillCatalog: ISkillCatalog, ISkillCatalogContextSource
{
    /// <summary>Initializes and captures configured definitions.</summary>
    /// <param name="options">The composition options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Definitions repeat.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A text bound is not positive or a definition exceeds it.</exception>
    public ConfiguredSkillCatalog(IOptions<SkillToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumNameCharacters, nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumDescriptionCharacters, nameof(options));
        var skills = options.Value.Skills.ToImmutableArray();
        ArgumentException.ThrowIfContainsNull(skills, nameof(options));
        foreach (var skill in skills)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(skill.Name.Length, options.Value.MaximumNameCharacters, nameof(options));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(skill.Description.Length, options.Value.MaximumDescriptionCharacters, nameof(options));
        }

        var metadata = JsonSerializer.SerializeToUtf8Bytes(skills.Select(static skill => new
        {
            id = skill.Id.Value,
            skill.Name,
            skill.Description,
            trust = skill.Trust.ToString(),
            expectedContentHash = skill.ExpectedContentHash?.Value,
        }));
        Snapshot = new SkillCatalogSnapshot(Convert.ToHexStringLower(SHA256.HashData(metadata)), skills);
    }

    /// <inheritdoc/>
    public SkillCatalogSnapshot Snapshot { get; }

    /// <inheritdoc/>
    public string CatalogVersion => Snapshot.Version;

    /// <inheritdoc/>
    public string RenderInventory() => JsonSerializer.Serialize(new
    {
        catalog_version = Snapshot.Version,
        instruction_authority = false,
        skills = Snapshot.Skills.Select(static skill => new { id = skill.Id.Value, skill.Name, skill.Description, trust = skill.Trust.ToString() }),
    });
}
