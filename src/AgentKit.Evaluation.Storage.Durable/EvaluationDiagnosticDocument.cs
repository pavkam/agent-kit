// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationDiagnostic"/>.</summary>
/// <param name="Code">The stable code.</param>
/// <param name="SafeMessage">The safe explanation.</param>
internal sealed record EvaluationDiagnosticDocument(string Code, string SafeMessage)
{
    /// <summary>Converts a diagnostic to its persisted form.</summary>
    /// <param name="value">The non-null diagnostic.</param>
    /// <returns>The document.</returns>
    internal static EvaluationDiagnosticDocument FromDomain(EvaluationDiagnostic value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Code, value.SafeMessage);
    }

    /// <summary>Restores the diagnostic, re-running its validation.</summary>
    /// <returns>The diagnostic.</returns>
    internal EvaluationDiagnostic ToDomain() => new(Code, SafeMessage);
}
