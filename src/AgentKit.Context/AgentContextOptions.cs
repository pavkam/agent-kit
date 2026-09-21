// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Mutable configuration for first-party context assembly and budgeting.</summary>
public sealed class AgentContextOptions
{
    /// <summary>Gets or sets how mandatory budget overflow is handled.</summary>
    public ContextOverflowBehavior OverflowBehavior { get; set; } = ContextOverflowBehavior.Fail;

    /// <summary>Gets or sets the minimum output tokens reserved before selecting context content.</summary>
    public int ReservedOutputTokens { get; set; } = 1_024;

    /// <summary>Gets or sets the estimated provider framing and schema overhead in tokens.</summary>
    public int ProviderOverheadTokens { get; set; } = 256;

    /// <summary>Gets or sets the fractional safety margin applied to the effective input budget.</summary>
    public double EstimationSafetyMargin { get; set; } = 0.10;

    /// <summary>Gets or sets the character-per-token ratio used for advisory estimation.</summary>
    public double EstimatedCharactersPerToken { get; set; } = 4.0;

    /// <summary>Gets or sets whether independent contributors may run concurrently when declared safe.</summary>
    public bool AllowParallelContributors { get; set; }
}
