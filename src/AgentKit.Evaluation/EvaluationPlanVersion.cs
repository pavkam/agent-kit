// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Versions the membership and content of one evaluation plan.</summary>
/// <remarks>Any change to cases, inputs, criteria, fixtures, or evaluator references takes a new version, so results from different versions are never blended into one cohort.</remarks>
public readonly record struct EvaluationPlanVersion
{
    /// <summary>Initializes a positive version.</summary>
    /// <param name="value">The positive monotonically increasing revision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public EvaluationPlanVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive revision.</summary>
    /// <value>The revision, or zero for a default instance that no consumer accepts.</value>
    public long Value { get; }

    /// <summary>Returns the invariant decimal text form.</summary>
    /// <returns>The revision in invariant culture.</returns>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
