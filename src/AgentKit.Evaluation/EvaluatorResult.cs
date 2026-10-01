// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records what one evaluator, at one version, concluded about one case repetition.</summary>
public sealed record EvaluatorResult
{
    /// <summary>Initializes a validated result.</summary>
    /// <param name="key">The evaluator key.</param>
    /// <param name="version">The evaluator version that produced the outcome.</param>
    /// <param name="outcome">The typed outcome, which carries the evidence the evaluator recorded.</param>
    /// <param name="duration">The non-negative evaluation time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outcome"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not positive or <paramref name="duration"/> is negative.</exception>
    public EvaluatorResult(
        EvaluatorKey key,
        EvaluatorVersion version,
        EvaluationOutcome outcome,
        TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(version));
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        Key = key;
        Version = version;
        Outcome = outcome;
        Duration = duration;
    }

    /// <summary>Gets the evaluator key.</summary>
    public EvaluatorKey Key { get; }

    /// <summary>Gets the evaluator version that produced the outcome.</summary>
    public EvaluatorVersion Version { get; }

    /// <summary>Gets the typed outcome, including its recorded evidence.</summary>
    public EvaluationOutcome Outcome { get; }

    /// <summary>Gets the evaluation time.</summary>
    public TimeSpan Duration { get; }
}
