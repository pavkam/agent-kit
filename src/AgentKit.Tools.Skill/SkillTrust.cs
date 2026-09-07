// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Records skill provenance without itself granting instruction authority.</summary>
public enum SkillTrust
{
    /// <summary>Managed host-owned content.</summary>
    Managed,
    /// <summary>User-owned content outside the project.</summary>
    User,
    /// <summary>Installed package content.</summary>
    Package,
    /// <summary>Repository-controlled content admitted by project trust.</summary>
    Workspace,
}
