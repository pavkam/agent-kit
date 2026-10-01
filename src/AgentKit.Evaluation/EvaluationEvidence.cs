// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records one named, safe fact an evaluator observed or configured, such as a judge model or rubric hash.</summary>
/// <remarks>Evidence is bounded text chosen by the evaluator author. It is persisted verbatim, so evaluators never place prompts, model output, or secrets in it unless the recorded content is an authored, classified configuration value such as a rubric.</remarks>
public sealed record EvaluationEvidence
{
    /// <summary>Initializes validated evidence.</summary>
    /// <param name="name">The non-blank evidence name.</param>
    /// <param name="value">The non-null evidence value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public EvaluationEvidence(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        Value = value;
    }

    /// <summary>Gets the evidence name.</summary>
    public string Name { get; }

    /// <summary>Gets the evidence value.</summary>
    public string Value { get; }
}
