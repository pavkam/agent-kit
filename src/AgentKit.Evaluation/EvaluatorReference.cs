// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Names one evaluator a case runs and optionally pins the evaluator version it expects.</summary>
public sealed record EvaluatorReference
{
    /// <summary>Initializes a validated evaluator reference.</summary>
    /// <param name="key">The non-blank evaluator key.</param>
    /// <param name="requiredVersion">The exact evaluator version the plan was authored against, or <see langword="null"/> to accept the registered version.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requiredVersion"/> is present but not positive.</exception>
    public EvaluatorReference(EvaluatorKey key, EvaluatorVersion? requiredVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        if (requiredVersion is { } version)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version.Value, nameof(requiredVersion));
        }

        Key = key;
        RequiredVersion = requiredVersion;
    }

    /// <summary>Gets the evaluator key.</summary>
    public EvaluatorKey Key { get; }

    /// <summary>Gets the pinned evaluator version, or <see langword="null"/> when any registered version is accepted.</summary>
    /// <value>A pinned version that differs from the registered evaluator fails plan validation before any case runs.</value>
    public EvaluatorVersion? RequiredVersion { get; }
}
