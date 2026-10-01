// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Configures the model judge evaluator at registration.</summary>
/// <remarks>No judge model is assumed: <see cref="JudgeModel"/> must be set explicitly, and the budget is bounded by default so an unattended run cannot spend without limit.</remarks>
public sealed class ModelJudgeOptions
{
    /// <summary>Gets or sets the explicit judge model alias, which must exist in the model catalog.</summary>
    /// <value>Unset by default; registration fails until it is named.</value>
    public ModelAlias JudgeModel { get; set; }

    /// <summary>Gets or sets the number of samples per judgement.</summary>
    /// <value>A positive count. The default is three, so the standard deviation is meaningful.</value>
    public int RepeatCount { get; set; } = 3;

    /// <summary>Gets or sets the greatest number of judge model requests across the evaluator lifetime.</summary>
    /// <value>A positive bound. The default is 300.</value>
    public int MaximumCalls { get; set; } = 300;

    /// <summary>Gets or sets the greatest number of reported tokens across the evaluator lifetime, or <see langword="null"/> for none.</summary>
    /// <value>A positive bound or <see langword="null"/>. The default is one million tokens.</value>
    public long? MaximumTokens { get; set; } = 1_000_000;

    /// <summary>Gets or sets the greatest tolerated sample standard deviation of normalized scores.</summary>
    /// <value>A value from zero to one. The default is 0.25.</value>
    public double MaximumStandardDeviation { get; set; } = 0.25;

    /// <summary>Gets or sets the greatest candidate length the judge is asked to assess.</summary>
    /// <value>A positive bound. The default is 16000 characters.</value>
    public int MaximumCandidateCharacters { get; set; } = 16_000;
}
