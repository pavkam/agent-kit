// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

/// <summary>Classifies the intended use of one configured resource without granting instruction authority.</summary>
public enum ResourceKind
{
    /// <summary>Human or generated documentation.</summary>
    Documentation,
    /// <summary>Reusable skill guidance.</summary>
    Skill,
    /// <summary>A prompt template that remains data until an explicit expansion pipeline selects it.</summary>
    Prompt,
    /// <summary>Project or application configuration.</summary>
    Configuration,
    /// <summary>Project instructions whose precedence is decided separately by context assembly.</summary>
    Instructions,
    /// <summary>Another textual resource.</summary>
    Other,
}
