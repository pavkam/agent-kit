// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluatorResult"/>.</summary>
/// <param name="Key">The evaluator key.</param>
/// <param name="Version">The evaluator version.</param>
/// <param name="Outcome">The outcome.</param>
/// <param name="Duration">The evaluation time.</param>
internal sealed record EvaluatorResultDocument(string Key, long Version, EvaluationOutcomeDocument Outcome, TimeSpan Duration)
{
    /// <summary>Converts a result to its persisted form.</summary>
    /// <param name="value">The non-null result.</param>
    /// <returns>The document.</returns>
    internal static EvaluatorResultDocument FromDomain(EvaluatorResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Key.Value, value.Version.Value, EvaluationOutcomeDocument.FromDomain(value.Outcome), value.Duration);
    }

    /// <summary>Restores the result, re-running its validation.</summary>
    /// <returns>The result.</returns>
    internal EvaluatorResult ToDomain() => new(new EvaluatorKey(Key), new EvaluatorVersion(Version), Outcome.ToDomain(), Duration);
}
