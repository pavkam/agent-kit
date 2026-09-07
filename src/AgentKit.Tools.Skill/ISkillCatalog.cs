// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Exposes the immutable skill snapshot captured for one composition.</summary>
public interface ISkillCatalog: ISkillCatalogContextSource
{
    /// <summary>Gets the snapshot used by both discovery context and the activation tool.</summary>
    public SkillCatalogSnapshot Snapshot { get; }
}
