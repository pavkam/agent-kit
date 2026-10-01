// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationScore"/>.</summary>
/// <param name="Value">The normalized score.</param>
/// <param name="SampleCount">The number of samples behind the score.</param>
/// <param name="StandardDeviation">The sample standard deviation.</param>
internal sealed record EvaluationScoreDocument(double Value, int SampleCount, double StandardDeviation)
{
    /// <summary>Converts a score to its persisted form.</summary>
    /// <param name="value">The non-null score.</param>
    /// <returns>The document.</returns>
    internal static EvaluationScoreDocument FromDomain(EvaluationScore value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Value, value.SampleCount, value.StandardDeviation);
    }

    /// <summary>Restores the score, re-running its validation.</summary>
    /// <returns>The score.</returns>
    internal EvaluationScore ToDomain() => new(Value, SampleCount, StandardDeviation);
}
