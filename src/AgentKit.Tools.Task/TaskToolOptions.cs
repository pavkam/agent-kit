// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task;

/// <summary>Configures model-facing delegation bounds below host and goal-profile ceilings.</summary>
public sealed class TaskToolOptions
{
    /// <summary>Gets or sets the default child settlement timeout.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);
    /// <summary>Gets or sets the maximum child settlement timeout.</summary>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(30);
    /// <summary>Gets or sets the default child turn ceiling.</summary>
    public int DefaultMaximumTurns { get; set; } = 20;
    /// <summary>Gets or sets the host child turn ceiling.</summary>
    public int MaximumTurns { get; set; } = 100;
    /// <summary>Gets or sets the default child tool-call ceiling.</summary>
    public int DefaultMaximumToolCalls { get; set; } = 50;
    /// <summary>Gets or sets the host child tool-call ceiling.</summary>
    public int MaximumToolCalls { get; set; } = 200;
    /// <summary>Gets or sets the maximum objective characters.</summary>
    public int MaximumObjectiveCharacters { get; set; } = 8_000;
    /// <summary>Gets or sets the maximum number of acceptance criteria.</summary>
    public int MaximumAcceptanceCriteria { get; set; } = 20;
    /// <summary>Gets or sets the maximum characters per acceptance criterion.</summary>
    public int MaximumCriterionCharacters { get; set; } = 1_000;
    /// <summary>Gets or sets the maximum number of child tools a model may select.</summary>
    public int MaximumAllowedTools { get; set; } = 64;
    /// <summary>Gets or sets the maximum child summary characters projected to the parent.</summary>
    public int MaximumSummaryCharacters { get; set; } = 16_000;
}
