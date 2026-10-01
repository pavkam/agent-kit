// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the base of one typed expected criterion a case carries for evaluators to assess.</summary>
/// <remarks>
/// A criterion is authored dataset data, never a grant of authority. Evaluators declare the <see cref="EvaluationCriterionKey"/>
/// values they support in their <see cref="EvaluatorDescriptor"/>; a third-party package defines its own criterion by deriving
/// a sealed record and choosing a unique key. Criteria are not persisted by result stores; the persisted result records which
/// evaluators ran and what they concluded.
/// </remarks>
public abstract record EvaluationCriterion
{
    /// <summary>Initializes the base with the criterion kind.</summary>
    /// <param name="key">The non-blank criterion key.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    protected EvaluationCriterion(EvaluationCriterionKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        Key = key;
    }

    /// <summary>Gets the criterion kind.</summary>
    public EvaluationCriterionKey Key { get; }
}
