// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationOutcome"/>, discriminated by its bounded variant name.</summary>
/// <param name="Kind">The variant name: <c>passed</c>, <c>failed</c>, <c>inconclusive</c>, <c>skipped</c>, <c>cancelled</c>, <c>unsupported</c>, or <c>evaluator_failed</c>.</param>
/// <param name="Summary">The safe explanation.</param>
/// <param name="Score">The score, or <see langword="null"/>.</param>
/// <param name="ErrorType">The normalized exception type of an evaluator failure, otherwise <see langword="null"/>.</param>
/// <param name="Evidence">The recorded evidence.</param>
internal sealed record EvaluationOutcomeDocument(
    string Kind,
    string Summary,
    EvaluationScoreDocument? Score,
    string? ErrorType,
    ImmutableArray<EvaluationEvidenceDocument> Evidence)
{
    /// <summary>Converts an outcome to its persisted form.</summary>
    /// <param name="value">The non-null outcome.</param>
    /// <returns>The document.</returns>
    internal static EvaluationOutcomeDocument FromDomain(EvaluationOutcome value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Name,
            value.Summary,
            value.Score is null ? null : EvaluationScoreDocument.FromDomain(value.Score),
            (value as EvaluatorFaulted)?.ErrorType,
            [.. value.Evidence.Select(EvaluationEvidenceDocument.FromDomain)]);
    }

    /// <summary>Restores the outcome, re-running its validation.</summary>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidDataException">The variant name is not recognized.</exception>
    internal EvaluationOutcome ToDomain()
    {
        var score = Score?.ToDomain();
        ImmutableArray<EvaluationEvidence> evidence = Evidence.IsDefault ? [] : [.. Evidence.Select(static item => item.ToDomain())];
        return Kind switch
        {
            "passed" => new EvaluationPassed(score, Summary, evidence),
            "failed" => new EvaluationFailed(score, Summary, evidence),
            "inconclusive" => new EvaluationInconclusive(score, Summary, evidence),
            "skipped" => new EvaluationSkipped(Summary, evidence),
            "cancelled" => new EvaluationCancelled(Summary, evidence),
            "unsupported" => new EvaluationUnsupported(Summary, evidence),
            "evaluator_failed" => new EvaluatorFaulted(ErrorType ?? throw new InvalidDataException("An evaluator failure carries no error type."), Summary, evidence),
            _ => throw new InvalidDataException("A persisted evaluation outcome has an unrecognized variant."),
        };
    }
}
