// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Configures a captured skill catalog and activation bounds.</summary>
public sealed class SkillToolOptions
{
    /// <summary>Gets the ordered skill definitions.</summary>
    public IList<SkillDefinition> Skills { get; } = [];
    /// <summary>Gets or sets the maximum complete backing-file bytes.</summary>
    public long MaximumBytes { get; set; } = 1_048_576;
    /// <summary>Gets or sets the maximum decoded characters returned to the model.</summary>
    public int MaximumCharacters { get; set; } = 200_000;
    /// <summary>Gets or sets the maximum name characters.</summary>
    public int MaximumNameCharacters { get; set; } = 200;
    /// <summary>Gets or sets the maximum description characters.</summary>
    public int MaximumDescriptionCharacters { get; set; } = 2_000;
}
