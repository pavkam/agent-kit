// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the complete recorded evidence of one case repetition: identities, manifest, usage, latency, trace, and evaluator outcomes.</summary>
/// <remarks>
/// <para>
/// The result preserves the typed <see cref="RunId"/> and <see cref="SessionId"/>, the trace correlation, the configuration
/// and model manifest, usage, and safe diagnostics. It is a bounded projection: prompts, model output, and tool data are not
/// part of it, so a result store never becomes a second copy of session history.
/// </para>
/// <para>A result is identified by its evaluation run, case ordinal, and repetition. Stores order results by case ordinal then repetition, which is plan order and therefore deterministic even when execution was parallel.</para>
/// </remarks>
public sealed record EvaluationCaseResult
{
    /// <summary>Initializes a validated result.</summary>
    /// <param name="evaluationRunId">The evaluation run.</param>
    /// <param name="planId">The plan.</param>
    /// <param name="planVersion">The plan version.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="caseOrdinal">The zero-based position of the case in the plan.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="disposition">How the repetition ended.</param>
    /// <param name="startedAt">When the repetition started.</param>
    /// <param name="latency">The non-negative elapsed time of the repetition.</param>
    /// <param name="traceId">The W3C trace identity of the case activity, or <see langword="null"/> when no listener sampled it.</param>
    /// <param name="run">The agent run record; required when <paramref name="disposition"/> is <see cref="EvaluationCaseDisposition.Evaluated"/>.</param>
    /// <param name="manifest">The agent and model manifest.</param>
    /// <param name="usage">The usage summary.</param>
    /// <param name="fixture">The fixture reference, or <see langword="null"/>.</param>
    /// <param name="evaluators">The evaluator results in declared order; may be empty.</param>
    /// <param name="diagnostics">The safe diagnostics; may be empty.</param>
    /// <exception cref="ArgumentNullException">A required reference is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or version is default, an ordinal or repetition is out of range, the disposition is undefined, or the latency is negative.</exception>
    /// <exception cref="ArgumentException">An array is default or contains null, the trace identity is blank, or an evaluated repetition has no run record.</exception>
    public EvaluationCaseResult(
        EvaluationRunId evaluationRunId,
        EvaluationPlanId planId,
        EvaluationPlanVersion planVersion,
        EvaluationCaseId caseId,
        int caseOrdinal,
        int repetition,
        EvaluationCaseDisposition disposition,
        DateTimeOffset startedAt,
        TimeSpan latency,
        string? traceId,
        EvaluationRunRecord? run,
        EvaluationRunManifest manifest,
        EvaluationUsageSummary usage,
        EvaluationFixtureReference? fixture,
        ImmutableArray<EvaluatorResult> evaluators,
        ImmutableArray<EvaluationDiagnostic> diagnostics)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(evaluationRunId, default, nameof(evaluationRunId));
        ArgumentException.ThrowIfNullOrWhiteSpace(planId.Value, nameof(planId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(planVersion.Value, nameof(planVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId.Value, nameof(caseId));
        ArgumentOutOfRangeException.ThrowIfNegative(caseOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetition);
        ArgumentOutOfRangeException.ThrowIfUndefined(disposition);
        ArgumentOutOfRangeException.ThrowIfLessThan(latency, TimeSpan.Zero);
        if (traceId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        }

        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentException.ThrowIfDefault(evaluators);
        ArgumentException.ThrowIfContainsNull(evaluators);
        ArgumentException.ThrowIfDefault(diagnostics);
        ArgumentException.ThrowIfContainsNull(diagnostics);
        if (disposition == EvaluationCaseDisposition.Evaluated)
        {
            ArgumentNullException.ThrowIfNull(run);
        }

        EvaluationRunId = evaluationRunId;
        PlanId = planId;
        PlanVersion = planVersion;
        CaseId = caseId;
        CaseOrdinal = caseOrdinal;
        Repetition = repetition;
        Disposition = disposition;
        StartedAt = startedAt;
        Latency = latency;
        TraceId = traceId;
        Run = run;
        Manifest = manifest;
        Usage = usage;
        Fixture = fixture;
        Evaluators = evaluators;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the evaluation run.</summary>
    public EvaluationRunId EvaluationRunId { get; }

    /// <summary>Gets the plan.</summary>
    public EvaluationPlanId PlanId { get; }

    /// <summary>Gets the plan version.</summary>
    public EvaluationPlanVersion PlanVersion { get; }

    /// <summary>Gets the case.</summary>
    public EvaluationCaseId CaseId { get; }

    /// <summary>Gets the zero-based position of the case in the plan.</summary>
    public int CaseOrdinal { get; }

    /// <summary>Gets the one-based repetition.</summary>
    public int Repetition { get; }

    /// <summary>Gets how the repetition ended.</summary>
    public EvaluationCaseDisposition Disposition { get; }

    /// <summary>Gets when the repetition started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets the elapsed time of the repetition.</summary>
    public TimeSpan Latency { get; }

    /// <summary>Gets the trace identity of the case activity, or <see langword="null"/>.</summary>
    /// <remarks>Trace identity is correlation evidence only; it is never semantic state.</remarks>
    public string? TraceId { get; }

    /// <summary>Gets the agent run record, or <see langword="null"/> when no run was produced.</summary>
    public EvaluationRunRecord? Run { get; }

    /// <summary>Gets the agent and model manifest.</summary>
    public EvaluationRunManifest Manifest { get; }

    /// <summary>Gets the usage summary.</summary>
    public EvaluationUsageSummary Usage { get; }

    /// <summary>Gets the fixture reference, or <see langword="null"/>.</summary>
    public EvaluationFixtureReference? Fixture { get; }

    /// <summary>Gets the evaluator results in declared order.</summary>
    public ImmutableArray<EvaluatorResult> Evaluators { get; }

    /// <summary>Gets the safe diagnostics.</summary>
    public ImmutableArray<EvaluationDiagnostic> Diagnostics { get; }

    /// <summary>Gets the honest summary of the evaluator outcomes.</summary>
    /// <value>Computed from <see cref="Disposition"/> and <see cref="Evaluators"/>; it is never stored.</value>
    public EvaluationVerdict Verdict
    {
        get
        {
            if (Disposition != EvaluationCaseDisposition.Evaluated)
            {
                return EvaluationVerdict.NotEvaluated;
            }

            var passed = false;
            var undecided = false;
            foreach (var evaluator in Evaluators)
            {
                switch (evaluator.Outcome)
                {
                    case EvaluationFailed:
                        return EvaluationVerdict.Failed;
                    case EvaluationPassed:
                        passed = true;
                        break;
                    case EvaluationSkipped:
                        break;
                    default:
                        undecided = true;
                        break;
                }
            }

            return undecided
                ? EvaluationVerdict.Inconclusive
                : passed ? EvaluationVerdict.Passed : EvaluationVerdict.NotEvaluated;
        }
    }

    /// <inheritdoc/>
    public bool Equals(EvaluationCaseResult? other) =>
        other is not null
        && EvaluationRunId == other.EvaluationRunId
        && PlanId == other.PlanId
        && PlanVersion == other.PlanVersion
        && CaseId == other.CaseId
        && CaseOrdinal == other.CaseOrdinal
        && Repetition == other.Repetition
        && Disposition == other.Disposition
        && StartedAt == other.StartedAt
        && Latency == other.Latency
        && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal)
        && Run == other.Run
        && Manifest == other.Manifest
        && Usage == other.Usage
        && Fixture == other.Fixture
        && Evaluators.SequenceEqual(other.Evaluators)
        && Diagnostics.SequenceEqual(other.Diagnostics);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EvaluationRunId);
        hash.Add(PlanId);
        hash.Add(PlanVersion);
        hash.Add(CaseId);
        hash.Add(CaseOrdinal);
        hash.Add(Repetition);
        hash.Add(Disposition);
        hash.Add(StartedAt);
        hash.Add(Latency);
        hash.Add(TraceId, StringComparer.Ordinal);
        hash.Add(Run);
        hash.Add(Manifest);
        hash.Add(Usage);
        hash.Add(Fixture);
        foreach (var evaluator in Evaluators)
        {
            hash.Add(evaluator);
        }

        foreach (var diagnostic in Diagnostics)
        {
            hash.Add(diagnostic);
        }

        return hash.ToHashCode();
    }
}
