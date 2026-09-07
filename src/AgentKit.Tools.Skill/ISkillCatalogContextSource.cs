// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Renders bounded skill discovery data from the exact catalog used for activation.</summary>
public interface ISkillCatalogContextSource
{
    /// <summary>Gets the captured catalog version.</summary>
    public string CatalogVersion { get; }

    /// <summary>Renders non-authoritative discovery text without reading skill bodies.</summary>
    /// <returns>Bounded text listing stable identities, names, descriptions, and provenance.</returns>
    public string RenderInventory();
}
