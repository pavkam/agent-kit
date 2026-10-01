// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports a normalized score together with the uncertainty of the sampling that produced it.</summary>
/// <remarks>Deterministic evaluators report a certain score of zero or one from a single sample. A model judge reports the mean of its repeated samples and their sample standard deviation so a reader can tell a stable verdict from a noisy one.</remarks>
public sealed record EvaluationScore
{
    /// <summary>Initializes a validated score.</summary>
    /// <param name="value">The finite score from zero to one inclusive.</param>
    /// <param name="sampleCount">The positive number of samples behind the score.</param>
    /// <param name="standardDeviation">The finite non-negative sample standard deviation, zero for a single sample.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside zero to one or not finite, <paramref name="sampleCount"/> is not positive, or <paramref name="standardDeviation"/> is negative or not finite.</exception>
    public EvaluationScore(double value, int sampleCount, double standardDeviation)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A score must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(value, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1d);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
        if (!double.IsFinite(standardDeviation))
        {
            throw new ArgumentOutOfRangeException(nameof(standardDeviation), standardDeviation, "A standard deviation must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(standardDeviation);
        Value = value;
        SampleCount = sampleCount;
        StandardDeviation = standardDeviation;
    }

    /// <summary>Gets the score from zero to one inclusive.</summary>
    public double Value { get; }

    /// <summary>Gets the number of samples behind the score.</summary>
    public int SampleCount { get; }

    /// <summary>Gets the sample standard deviation.</summary>
    public double StandardDeviation { get; }

    /// <summary>Creates the score of a deterministic single-sample judgement.</summary>
    /// <param name="passed">Whether the single check passed.</param>
    /// <returns>A score of one or zero with one sample and no deviation.</returns>
    public static EvaluationScore Certain(bool passed) => new(passed ? 1d : 0d, 1, 0d);
}
