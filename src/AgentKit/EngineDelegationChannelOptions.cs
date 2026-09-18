// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds for <see cref="EngineDelegationChannel"/>.</summary>
public sealed class EngineDelegationChannelOptions
{
    /// <summary>Gets or sets the maximum number of characters of the child's answer returned to the parent.</summary>
    /// <value>16,000 by default; must be positive. Longer answers are truncated, never rejected.</value>
    public int MaximumSummaryCharacters { get; set; } = 16_000;
}
