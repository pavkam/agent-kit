// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the validated, immutable configuration of one <see cref="ModelJudgeEvaluator"/>.</summary>
/// <remarks>The judge model is an explicit alias: there is no default model and no fallback to the model under test.</remarks>
public sealed record ModelJudgeSettings
{
    /// <summary>Initializes validated settings.</summary>
    /// <param name="judgeModel">The explicit non-blank judge model alias.</param>
    /// <param name="repeatCount">The positive number of samples per judgement.</param>
    /// <param name="budget">The separate judge budget.</param>
    /// <param name="maximumStandardDeviation">The greatest tolerated sample standard deviation of normalized scores, from zero to one; a noisier judgement is inconclusive.</param>
    /// <param name="maximumCandidateCharacters">The positive greatest candidate length the judge is asked to assess; longer text is inconclusive instead of silently truncated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="budget"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="judgeModel"/> is default or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count or bound is out of range or not finite.</exception>
    public ModelJudgeSettings(
        ModelAlias judgeModel,
        int repeatCount,
        ModelJudgeBudget budget,
        double maximumStandardDeviation,
        int maximumCandidateCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(judgeModel.Value, nameof(judgeModel));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repeatCount);
        ArgumentNullException.ThrowIfNull(budget);
        if (!double.IsFinite(maximumStandardDeviation))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStandardDeviation), maximumStandardDeviation, "A standard-deviation bound must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maximumStandardDeviation, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumStandardDeviation, 1d);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCandidateCharacters);
        JudgeModel = judgeModel;
        RepeatCount = repeatCount;
        Budget = budget;
        MaximumStandardDeviation = maximumStandardDeviation;
        MaximumCandidateCharacters = maximumCandidateCharacters;
    }

    /// <summary>Gets the explicit judge model alias.</summary>
    public ModelAlias JudgeModel { get; }

    /// <summary>Gets the number of samples per judgement.</summary>
    public int RepeatCount { get; }

    /// <summary>Gets the separate judge budget.</summary>
    public ModelJudgeBudget Budget { get; }

    /// <summary>Gets the greatest tolerated sample standard deviation of normalized scores.</summary>
    public double MaximumStandardDeviation { get; }

    /// <summary>Gets the greatest candidate length the judge is asked to assess.</summary>
    public int MaximumCandidateCharacters { get; }
}
