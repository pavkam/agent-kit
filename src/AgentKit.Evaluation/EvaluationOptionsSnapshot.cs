// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the validated, immutable copy of <see cref="EvaluationOptions"/> a runner holds for its lifetime.</summary>
internal sealed record EvaluationOptionsSnapshot
{
    /// <summary>Initializes validated finite mechanics.</summary>
    /// <param name="maximumConcurrentCases">The positive cap on concurrent case repetitions.</param>
    /// <param name="maximumRepetitions">The positive cap on repetitions per case.</param>
    /// <param name="defaultCaseTimeout">The positive default per-repetition time limit and recording-write bound.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is not positive.</exception>
    internal EvaluationOptionsSnapshot(int maximumConcurrentCases, int maximumRepetitions, TimeSpan defaultCaseTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentCases);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRepetitions);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(defaultCaseTimeout, TimeSpan.Zero);
        MaximumConcurrentCases = maximumConcurrentCases;
        MaximumRepetitions = maximumRepetitions;
        DefaultCaseTimeout = defaultCaseTimeout;
    }

    /// <summary>Gets the cap on concurrent case repetitions.</summary>
    internal int MaximumConcurrentCases { get; }

    /// <summary>Gets the cap on repetitions per case.</summary>
    internal int MaximumRepetitions { get; }

    /// <summary>Gets the default per-repetition time limit and recording-write bound.</summary>
    internal TimeSpan DefaultCaseTimeout { get; }

    /// <summary>Copies and validates mutable options.</summary>
    /// <param name="options">The non-null options to capture.</param>
    /// <returns>The immutable snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured value is not positive.</exception>
    internal static EvaluationOptionsSnapshot From(EvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new EvaluationOptionsSnapshot(options.MaximumConcurrentCases, options.MaximumRepetitions, options.DefaultCaseTimeout);
    }
}
