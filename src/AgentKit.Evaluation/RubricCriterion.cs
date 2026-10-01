// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Expects the final output to satisfy a written rubric as judged by a model on a bounded integer scale.</summary>
/// <remarks>The rubric is authored dataset data. It is recorded verbatim with every judgement so a result always states exactly what the judge was asked.</remarks>
public sealed record RubricCriterion: EvaluationCriterion
{
    /// <summary>Gets the criterion key, <c>rubric</c>.</summary>
    public static EvaluationCriterionKey CriterionKey { get; } = new("rubric");

    /// <summary>Initializes a validated criterion.</summary>
    /// <param name="rubric">The non-blank rubric text the judge applies.</param>
    /// <param name="scaleMaximum">The positive highest score; zero always means the output fails the rubric entirely.</param>
    /// <param name="passThreshold">The normalized mean score from zero to one at or above which the output passes.</param>
    /// <exception cref="ArgumentException"><paramref name="rubric"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scaleMaximum"/> is not positive, or <paramref name="passThreshold"/> is outside zero to one or not finite.</exception>
    public RubricCriterion(string rubric, int scaleMaximum = 5, double passThreshold = 0.7)
        : base(CriterionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rubric);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleMaximum);
        if (!double.IsFinite(passThreshold))
        {
            throw new ArgumentOutOfRangeException(nameof(passThreshold), passThreshold, "A pass threshold must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(passThreshold, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(passThreshold, 1d);
        Rubric = rubric;
        ScaleMaximum = scaleMaximum;
        PassThreshold = passThreshold;
    }

    /// <summary>Gets the rubric text the judge applies.</summary>
    public string Rubric { get; }

    /// <summary>Gets the highest score on the judge scale.</summary>
    public int ScaleMaximum { get; }

    /// <summary>Gets the normalized mean score at or above which the output passes.</summary>
    public double PassThreshold { get; }
}
