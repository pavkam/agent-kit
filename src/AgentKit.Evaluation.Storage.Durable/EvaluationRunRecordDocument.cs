// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationRunRecord"/>.</summary>
/// <param name="RunId">The agent run identity.</param>
/// <param name="SessionId">The case session identity.</param>
/// <param name="Outcome">The bounded outcome name.</param>
/// <param name="Settlement">The bounded settlement name.</param>
internal sealed record EvaluationRunRecordDocument(Guid RunId, Guid SessionId, string Outcome, string Settlement)
{
    /// <summary>Converts a run record to its persisted form.</summary>
    /// <param name="value">The non-null record.</param>
    /// <returns>The document.</returns>
    internal static EvaluationRunRecordDocument FromDomain(EvaluationRunRecord value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.RunId.Value, value.SessionId.Value, value.Outcome, value.Settlement);
    }

    /// <summary>Restores the record, re-running its validation.</summary>
    /// <returns>The record.</returns>
    internal EvaluationRunRecord ToDomain() => new(new RunId(RunId), new SessionId(SessionId), Outcome, Settlement);
}
