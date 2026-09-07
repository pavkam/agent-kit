// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question;

/// <summary>Configures model-facing question shape and response-time ceilings.</summary>
public sealed class QuestionToolOptions
{
    /// <summary>Gets or sets the default response timeout.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the largest response timeout a model may request.</summary>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Gets or sets the maximum question characters.</summary>
    public int MaximumPromptCharacters { get; set; } = 4_000;

    /// <summary>Gets or sets the maximum option-label characters.</summary>
    public int MaximumLabelCharacters { get; set; } = 100;

    /// <summary>Gets or sets the maximum option-description characters.</summary>
    public int MaximumDescriptionCharacters { get; set; } = 500;

    /// <summary>Gets or sets the maximum free-text answer characters projected back to the model.</summary>
    public int MaximumAnswerCharacters { get; set; } = 4_000;
}
