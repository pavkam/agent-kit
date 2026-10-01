// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Describes one reason a plan cannot run, with a safe message that carries no dataset content.</summary>
public sealed record EvaluationPlanProblem
{
    /// <summary>Initializes a validated problem.</summary>
    /// <param name="kind">The problem class.</param>
    /// <param name="caseId">The case concerned, or <see langword="null"/> for a plan-wide problem.</param>
    /// <param name="safeMessage">The non-blank content-free explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="caseId"/> is present but blank, or <paramref name="safeMessage"/> is blank.</exception>
    public EvaluationPlanProblem(EvaluationPlanProblemKind kind, EvaluationCaseId? caseId, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        if (caseId is { } id)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(caseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        CaseId = caseId;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the problem class.</summary>
    public EvaluationPlanProblemKind Kind { get; }

    /// <summary>Gets the case concerned, or <see langword="null"/> for a plan-wide problem.</summary>
    public EvaluationCaseId? CaseId { get; }

    /// <summary>Gets the content-free explanation.</summary>
    public string SafeMessage { get; }
}
