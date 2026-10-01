// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.Evaluation;

/// <summary>Builds valid, fully typed evaluation results for the result-store suite.</summary>
public static class EvaluationResultConformanceData
{
    /// <summary>Creates a fresh evaluation run identity.</summary>
    /// <returns>A unique run identity.</returns>
    public static EvaluationRunId NewRun() => new(Guid.NewGuid());

    /// <summary>Creates a result with one passed evaluator.</summary>
    /// <param name="run">The evaluation run.</param>
    /// <param name="ordinal">The zero-based case position.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="caseId">The case identity; defaults to one derived from the ordinal.</param>
    /// <param name="plan">The plan identity.</param>
    /// <param name="version">The plan version.</param>
    /// <param name="latencyMilliseconds">The recorded latency, which lets a test make two results differ.</param>
    /// <returns>The result.</returns>
    public static EvaluationCaseResult Result(
        EvaluationRunId run,
        int ordinal = 0,
        int repetition = 1,
        string? caseId = null,
        string plan = "plan",
        long version = 1,
        double latencyMilliseconds = 25.5) =>
        new(
            run,
            new EvaluationPlanId(plan),
            new EvaluationPlanVersion(version),
            new EvaluationCaseId(caseId ?? $"case-{ordinal}"),
            ordinal,
            repetition,
            EvaluationCaseDisposition.Evaluated,
            DateTimeOffset.UnixEpoch.AddSeconds((ordinal * 100) + repetition),
            TimeSpan.FromMilliseconds(latencyMilliseconds),
            "0af7651916cd43dd8448eb211c80319c",
            new EvaluationRunRecord(new RunId(Stable(ordinal, repetition, 3)), new SessionId(Stable(ordinal, repetition, 2)), "succeeded", "completed"),
            Manifest(),
            new EvaluationUsageSummary(1, 10, 12),
            new EvaluationFixtureReference("fixture", "1"),
            [new EvaluatorResult(new EvaluatorKey("schema"), new EvaluatorVersion(1), new EvaluationPassed(EvaluationScore.Certain(true), "matched"), TimeSpan.FromMilliseconds(3))],
            [new EvaluationDiagnostic("note", "safe note")]);

    /// <summary>Creates a result that exercises every outcome variant and every optional member.</summary>
    /// <param name="run">The evaluation run.</param>
    /// <returns>The fully populated result.</returns>
    public static EvaluationCaseResult Complete(EvaluationRunId run)
    {
        EvaluationOutcome[] outcomes =
        [
            new EvaluationPassed(new EvaluationScore(0.875, 3, 0.0625), "passed", [new EvaluationEvidence("name", "value"), new EvaluationEvidence("empty", string.Empty)]),
            new EvaluationFailed(EvaluationScore.Certain(false), "failed"),
            new EvaluationInconclusive(new EvaluationScore(0.5, 2, 0.5), "inconclusive"),
            new EvaluationSkipped("skipped"),
            new EvaluationCancelled("cancelled"),
            new EvaluationUnsupported("unsupported"),
            new EvaluatorFaulted("System.InvalidOperationException", "faulted"),
        ];
        return new EvaluationCaseResult(
            run,
            new EvaluationPlanId("complete-plan"),
            new EvaluationPlanVersion(7),
            new EvaluationCaseId("complete-case"),
            3,
            2,
            EvaluationCaseDisposition.Evaluated,
            new DateTimeOffset(2026, 10, 1, 12, 30, 15, TimeSpan.FromHours(2)).AddTicks(1234567),
            TimeSpan.FromTicks(1234567),
            "0af7651916cd43dd8448eb211c80319c",
            new EvaluationRunRecord(new RunId(Stable(3, 2, 3)), new SessionId(Stable(3, 2, 2)), "policy_halted", "recovery_required"),
            new EvaluationRunManifest(
                new AgentId(Stable(0, 0, 1)),
                new AgentDefinitionRevision(4),
                new AgentCatalogVersion(9),
                new SessionProfileKey("profile"),
                ["chat", "fallback"],
                [new EvaluationModelUse("provider", "family", "model", "deployment"), new EvaluationModelUse("provider", "family", "other", null)]),
            new EvaluationUsageSummary(2, 120, null),
            new EvaluationFixtureReference("fixture-key", "3"),
            [.. outcomes.Select(static (outcome, index) => new EvaluatorResult(new EvaluatorKey($"evaluator-{index}"), new EvaluatorVersion(index + 1), outcome, TimeSpan.FromMilliseconds(index + 0.5)))],
            [new EvaluationDiagnostic("code", "safe message"), new EvaluationDiagnostic("second", "another")]);
    }

    /// <summary>Creates a result that was never evaluated, with no run record, fixture, or trace; it belongs to the same plan and version as <see cref="Complete"/> so both fit one run.</summary>
    /// <param name="run">The evaluation run.</param>
    /// <returns>The sparse result.</returns>
    public static EvaluationCaseResult Sparse(EvaluationRunId run) =>
        new(
            run,
            new EvaluationPlanId("complete-plan"),
            new EvaluationPlanVersion(7),
            new EvaluationCaseId("sparse"),
            0,
            1,
            EvaluationCaseDisposition.RunRejected,
            DateTimeOffset.UnixEpoch,
            TimeSpan.Zero,
            null,
            null,
            Manifest(),
            EvaluationUsageSummary.None,
            null,
            [],
            [new EvaluationDiagnostic("session_rejected", "refused")]);

    private static Guid Stable(int ordinal, int repetition, byte kind) => new(ordinal + 1, (short) repetition, 0, [0, 0, 0, 0, 0, 0, 0, kind]);

    private static EvaluationRunManifest Manifest() => new(
        new AgentId(Stable(0, 0, 1)),
        new AgentDefinitionRevision(1),
        new AgentCatalogVersion(1),
        new SessionProfileKey("session"),
        ["chat"],
        [new EvaluationModelUse("provider", "family", "model", null)]);
}
