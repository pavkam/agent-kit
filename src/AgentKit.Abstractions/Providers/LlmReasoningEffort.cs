// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the portable amount of reasoning work requested from a capable model.</summary>
/// <remarks>
/// Providers translate these semantic levels to their native request values. A model that does not advertise
/// reasoning support cannot satisfy a request that explicitly selects one of these levels.
/// </remarks>
public enum LlmReasoningEffort
{
    /// <summary>Requests the provider's lower reasoning-work setting.</summary>
    Low,

    /// <summary>Requests a balanced reasoning-work setting.</summary>
    Medium,

    /// <summary>Requests the provider's higher reasoning-work setting.</summary>
    High,

    /// <summary>Requests the provider's highest extended reasoning-work setting when available.</summary>
    ExtraHigh,

    /// <summary>Explicitly disables provider reasoning work instead of accepting the provider's default.</summary>
    /// <remarks>This differs from an unspecified effort, which leaves the provider free to choose its default.</remarks>
    None,
}
