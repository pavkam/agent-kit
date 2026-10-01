// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationEvidence"/>.</summary>
/// <param name="Name">The evidence name.</param>
/// <param name="Value">The evidence value.</param>
internal sealed record EvaluationEvidenceDocument(string Name, string Value)
{
    /// <summary>Converts evidence to its persisted form.</summary>
    /// <param name="value">The non-null evidence.</param>
    /// <returns>The document.</returns>
    internal static EvaluationEvidenceDocument FromDomain(EvaluationEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Name, value.Value);
    }

    /// <summary>Restores the evidence, re-running its validation.</summary>
    /// <returns>The evidence.</returns>
    internal EvaluationEvidence ToDomain() => new(Name, Value);
}
