// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the deterministic, immutable report of one evaluation run.</summary>
/// <remarks>
/// <para>
/// Results are ordered by plan case position then repetition regardless of parallel execution. A cancelled, expired, or
/// stopped run still produces a report holding exactly the repetitions that were recorded; the repetitions never scheduled are
/// counted in <see cref="NotStartedCaseRuns"/> rather than invented.
/// </para>
/// <para>Store appends and exports are separate effects. <see cref="StoreResults"/> and <see cref="ExportResults"/> record what each answered; an exporter receives the report before its own export is recorded, so its copy never lists exports.</para>
/// </remarks>
public sealed record EvaluationReport
{
    /// <summary>Initializes a validated report.</summary>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="planId">The plan.</param>
    /// <param name="planVersion">The plan version.</param>
    /// <param name="startedAt">When the run started.</param>
    /// <param name="completedAt">When the report was completed; not before <paramref name="startedAt"/>.</param>
    /// <param name="status">How the run ended.</param>
    /// <param name="results">The recorded repetitions in plan order.</param>
    /// <param name="notStartedCaseRuns">The non-negative number of repetitions never scheduled.</param>
    /// <param name="storeResults">What the result store answered for each append, in plan order; empty when no store was selected.</param>
    /// <param name="exportResults">What each exporter answered, in plan order; empty until exports are recorded.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or version is default, the status is undefined, <paramref name="notStartedCaseRuns"/> is negative, or <paramref name="completedAt"/> precedes <paramref name="startedAt"/>.</exception>
    /// <exception cref="ArgumentException">A plan identity is blank, an array is default or contains null, or a result belongs to another run or plan.</exception>
    public EvaluationReport(
        EvaluationRunId runId,
        EvaluationPlanId planId,
        EvaluationPlanVersion planVersion,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        EvaluationReportStatus status,
        ImmutableArray<EvaluationCaseResult> results,
        int notStartedCaseRuns,
        ImmutableArray<EvaluationStoreAppendRecord> storeResults,
        ImmutableArray<EvaluationExportRecord> exportResults)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(runId));
        ArgumentException.ThrowIfNullOrWhiteSpace(planId.Value, nameof(planId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(planVersion.Value, nameof(planVersion));
        ArgumentOutOfRangeException.ThrowIfLessThan(completedAt, startedAt, nameof(completedAt));
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(results);
        ArgumentException.ThrowIfContainsNull(results);
        ArgumentOutOfRangeException.ThrowIfNegative(notStartedCaseRuns);
        ArgumentException.ThrowIfDefault(storeResults);
        ArgumentException.ThrowIfContainsNull(storeResults);
        ArgumentException.ThrowIfDefault(exportResults);
        ArgumentException.ThrowIfContainsNull(exportResults);
        foreach (var result in results)
        {
            ArgumentException.ThrowIfNotEqual(
                result.EvaluationRunId == runId && result.PlanId == planId && result.PlanVersion == planVersion, true, nameof(results));
        }

        RunId = runId;
        PlanId = planId;
        PlanVersion = planVersion;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Status = status;
        Results = results;
        NotStartedCaseRuns = notStartedCaseRuns;
        StoreResults = storeResults;
        ExportResults = exportResults;
    }

    /// <summary>Gets the evaluation run.</summary>
    public EvaluationRunId RunId { get; }

    /// <summary>Gets the plan.</summary>
    public EvaluationPlanId PlanId { get; }

    /// <summary>Gets the plan version.</summary>
    public EvaluationPlanVersion PlanVersion { get; }

    /// <summary>Gets when the run started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets when the report was completed.</summary>
    public DateTimeOffset CompletedAt { get; }

    /// <summary>Gets how the run ended.</summary>
    public EvaluationReportStatus Status { get; }

    /// <summary>Gets the recorded repetitions in plan order.</summary>
    public ImmutableArray<EvaluationCaseResult> Results { get; }

    /// <summary>Gets the number of repetitions never scheduled.</summary>
    public int NotStartedCaseRuns { get; }

    /// <summary>Gets what the result store answered for each append, in plan order.</summary>
    public ImmutableArray<EvaluationStoreAppendRecord> StoreResults { get; }

    /// <summary>Gets what each exporter answered, in plan order.</summary>
    public ImmutableArray<EvaluationExportRecord> ExportResults { get; }

    /// <summary>Gets the verdict counts.</summary>
    /// <value>Computed from <see cref="Results"/> and <see cref="NotStartedCaseRuns"/>; it is never stored.</value>
    public EvaluationSummary Summary
    {
        get
        {
            int passed = 0, failed = 0, inconclusive = 0, notEvaluated = 0;
            foreach (var result in Results)
            {
                switch (result.Verdict)
                {
                    case EvaluationVerdict.Passed:
                        passed++;
                        break;
                    case EvaluationVerdict.Failed:
                        failed++;
                        break;
                    case EvaluationVerdict.Inconclusive:
                        inconclusive++;
                        break;
                    case EvaluationVerdict.NotEvaluated:
                        notEvaluated++;
                        break;
                    default:
                        throw new InvalidOperationException("The evaluation verdict is undefined.");
                }
            }

            return new EvaluationSummary(passed, failed, inconclusive, notEvaluated, NotStartedCaseRuns);
        }
    }

    /// <summary>Returns a copy that records what each exporter answered.</summary>
    /// <param name="exportResults">The exporter answers in plan order.</param>
    /// <returns>A report identical to this one except for <see cref="ExportResults"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exportResults"/> is default or contains null.</exception>
    public EvaluationReport WithExportResults(ImmutableArray<EvaluationExportRecord> exportResults) =>
        new(RunId, PlanId, PlanVersion, StartedAt, CompletedAt, Status, Results, NotStartedCaseRuns, StoreResults, exportResults);

    /// <inheritdoc/>
    public bool Equals(EvaluationReport? other) =>
        other is not null
        && RunId == other.RunId
        && PlanId == other.PlanId
        && PlanVersion == other.PlanVersion
        && StartedAt == other.StartedAt
        && CompletedAt == other.CompletedAt
        && Status == other.Status
        && Results.SequenceEqual(other.Results)
        && NotStartedCaseRuns == other.NotStartedCaseRuns
        && StoreResults.SequenceEqual(other.StoreResults)
        && ExportResults.SequenceEqual(other.ExportResults);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RunId);
        hash.Add(PlanId);
        hash.Add(PlanVersion);
        hash.Add(StartedAt);
        hash.Add(CompletedAt);
        hash.Add(Status);
        foreach (var result in Results)
        {
            hash.Add(result);
        }

        hash.Add(NotStartedCaseRuns);
        foreach (var store in StoreResults)
        {
            hash.Add(store);
        }

        foreach (var export in ExportResults)
        {
            hash.Add(export);
        }

        return hash.ToHashCode();
    }
}
