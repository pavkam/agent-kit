// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Builds a fully populated result that proves an encoding contract round-trips every persisted shape exactly.</summary>
internal static class EvaluationResultProbe
{
    /// <summary>Creates a probe covering every outcome variant, score, evidence, usage, manifest, and optional member.</summary>
    /// <returns>The probe result.</returns>
    internal static EvaluationCaseResult Create()
    {
        var outcomes = new EvaluationOutcome[]
        {
            new EvaluationPassed(new EvaluationScore(0.875, 3, 0.0625), "passed", [new EvaluationEvidence("name", "value")]),
            new EvaluationFailed(EvaluationScore.Certain(false), "failed"),
            new EvaluationInconclusive(new EvaluationScore(0.5, 2, 0.5), "inconclusive"),
            new EvaluationSkipped("skipped"),
            new EvaluationCancelled("cancelled"),
            new EvaluationUnsupported("unsupported"),
            new EvaluatorFaulted("System.InvalidOperationException", "faulted"),
        };
        return new EvaluationCaseResult(
            new EvaluationRunId(Guid.Parse("e1000000-0000-0000-0000-0000000000f1")),
            new EvaluationPlanId("probe-plan"),
            new EvaluationPlanVersion(7),
            new EvaluationCaseId("probe-case"),
            3,
            2,
            EvaluationCaseDisposition.Evaluated,
            new DateTimeOffset(2026, 10, 1, 12, 30, 15, TimeSpan.FromHours(2)).AddTicks(1234567),
            TimeSpan.FromTicks(1234567),
            "0af7651916cd43dd8448eb211c80319c",
            new EvaluationRunRecord(
                new RunId(Guid.Parse("30000000-0000-0000-0000-0000000000f1")),
                new SessionId(Guid.Parse("20000000-0000-0000-0000-0000000000f1")),
                "succeeded",
                "completed"),
            new EvaluationRunManifest(
                new AgentId(Guid.Parse("a0000000-0000-0000-0000-0000000000f1")),
                new AgentDefinitionRevision(4),
                new AgentCatalogVersion(9),
                new SessionProfileKey("probe-session"),
                ["chat", "fallback"],
                [new EvaluationModelUse("provider", "family", "model", "deployment"), new EvaluationModelUse("provider", "family", "other", null)]),
            new EvaluationUsageSummary(2, 120, null),
            new EvaluationFixtureReference("probe-fixture", "3"),
            [.. outcomes.Select(static (outcome, index) => new EvaluatorResult(new EvaluatorKey($"probe-{index}"), new EvaluatorVersion(index + 1), outcome, TimeSpan.FromMilliseconds(index + 0.5)))],
            [new EvaluationDiagnostic("probe", "probe diagnostic")]);
    }
}
