// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationCaseResult"/>, one per JSON record or SQLite row.</summary>
/// <remarks>The document is a bounded projection of safe evidence. Converting back re-runs every domain validation, so a corrupted record fails closed instead of producing an invalid result.</remarks>
internal sealed record EvaluationResultDocument(
    Guid EvaluationRunId,
    string PlanId,
    long PlanVersion,
    string CaseId,
    int CaseOrdinal,
    int Repetition,
    EvaluationCaseDisposition Disposition,
    DateTimeOffset StartedAt,
    TimeSpan Latency,
    string? TraceId,
    EvaluationRunRecordDocument? Run,
    EvaluationManifestDocument Manifest,
    EvaluationUsageDocument Usage,
    EvaluationFixtureDocument? Fixture,
    ImmutableArray<EvaluatorResultDocument> Evaluators,
    ImmutableArray<EvaluationDiagnosticDocument> Diagnostics)
{
    /// <summary>Converts a result to its persisted form.</summary>
    /// <param name="value">The non-null result.</param>
    /// <returns>The document.</returns>
    internal static EvaluationResultDocument FromDomain(EvaluationCaseResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.EvaluationRunId.Value,
            value.PlanId.Value,
            value.PlanVersion.Value,
            value.CaseId.Value,
            value.CaseOrdinal,
            value.Repetition,
            value.Disposition,
            value.StartedAt,
            value.Latency,
            value.TraceId,
            value.Run is null ? null : EvaluationRunRecordDocument.FromDomain(value.Run),
            EvaluationManifestDocument.FromDomain(value.Manifest),
            EvaluationUsageDocument.FromDomain(value.Usage),
            value.Fixture is null ? null : EvaluationFixtureDocument.FromDomain(value.Fixture),
            [.. value.Evaluators.Select(EvaluatorResultDocument.FromDomain)],
            [.. value.Diagnostics.Select(EvaluationDiagnosticDocument.FromDomain)]);
    }

    /// <summary>Restores the result, re-running its validation.</summary>
    /// <returns>The result.</returns>
    internal EvaluationCaseResult ToDomain() => new(
        new EvaluationRunId(EvaluationRunId),
        new EvaluationPlanId(PlanId),
        new EvaluationPlanVersion(PlanVersion),
        new EvaluationCaseId(CaseId),
        CaseOrdinal,
        Repetition,
        Disposition,
        StartedAt,
        Latency,
        TraceId,
        Run?.ToDomain(),
        Manifest.ToDomain(),
        Usage.ToDomain(),
        Fixture?.ToDomain(),
        Evaluators.IsDefault ? [] : [.. Evaluators.Select(static item => item.ToDomain())],
        Diagnostics.IsDefault ? [] : [.. Diagnostics.Select(static item => item.ToDomain())]);
}
