// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Counts the verdicts of one report without blending incompatible outcomes into one score.</summary>
public sealed record EvaluationSummary
{
    /// <summary>Initializes a validated summary.</summary>
    /// <param name="passed">The non-negative number of passed repetitions.</param>
    /// <param name="failed">The non-negative number of failed repetitions.</param>
    /// <param name="inconclusive">The non-negative number of inconclusive repetitions.</param>
    /// <param name="notEvaluated">The non-negative number of repetitions that were not evaluated.</param>
    /// <param name="notStarted">The non-negative number of repetitions that were never scheduled.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public EvaluationSummary(int passed, int failed, int inconclusive, int notEvaluated, int notStarted)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(passed);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);
        ArgumentOutOfRangeException.ThrowIfNegative(inconclusive);
        ArgumentOutOfRangeException.ThrowIfNegative(notEvaluated);
        ArgumentOutOfRangeException.ThrowIfNegative(notStarted);
        Passed = passed;
        Failed = failed;
        Inconclusive = inconclusive;
        NotEvaluated = notEvaluated;
        NotStarted = notStarted;
    }

    /// <summary>Gets the number of passed repetitions.</summary>
    public int Passed { get; }

    /// <summary>Gets the number of failed repetitions.</summary>
    public int Failed { get; }

    /// <summary>Gets the number of inconclusive repetitions.</summary>
    public int Inconclusive { get; }

    /// <summary>Gets the number of repetitions that were not evaluated.</summary>
    public int NotEvaluated { get; }

    /// <summary>Gets the number of repetitions that were never scheduled.</summary>
    public int NotStarted { get; }

    /// <summary>Gets the number of repetitions with a recorded result.</summary>
    public int Recorded => Passed + Failed + Inconclusive + NotEvaluated;
}
