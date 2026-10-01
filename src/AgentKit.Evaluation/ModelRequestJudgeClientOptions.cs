// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Configures the first-party <see cref="ModelRequestJudgeClient"/>.</summary>
public sealed class ModelRequestJudgeClientOptions
{
    /// <summary>Gets or sets the time one judge request may take before its deadline passes.</summary>
    /// <value>A positive duration. The default is two minutes.</value>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets or sets the output token ceiling for one judge reply.</summary>
    /// <value>A positive bound. The default is 256, enough for the one-line JSON verdict.</value>
    public long MaximumOutputTokens { get; set; } = 256;

    /// <summary>Gets or sets the sampling temperature, or <see langword="null"/> for the provider default.</summary>
    /// <value>A value from zero to two or <see langword="null"/>. The default is zero for the most repeatable judgement.</value>
    public double? Temperature { get; set; } = 0d;
}
