// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Identifies one case inside an evaluation plan.</summary>
/// <remarks>The identity is stable across runs and repetitions so results can be compared case by case; it is unique within one plan. This immutable value uses ordinal, case-sensitive text equality, preserves the supplied text exactly without trimming or normalization, and identifies configuration or dataset content only; it neither creates work nor grants authority. A default instance carries no usable text, so a consumer must reject it at its own boundary.</remarks>
public readonly record struct EvaluationCaseId
{
    /// <summary>Initializes a validated key.</summary>
    /// <param name="value">The non-blank canonical key text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty or consists only of whitespace.</exception>
    public EvaluationCaseId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical key text.</summary>
    /// <value>The exact caller-supplied text. A default instance exposes <see langword="null"/> at runtime.</value>
    public string Value { get; }

    /// <summary>Returns the canonical key text for diagnostics and composition messages.</summary>
    /// <returns>The exact key text, or <see cref="string.Empty"/> for a default instance.</returns>
    public override string ToString() => Value ?? string.Empty;
}
