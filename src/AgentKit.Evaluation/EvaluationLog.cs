// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Defines content-free structured logs emitted by the evaluation runner.</summary>
/// <remarks>Event identifiers 36000-36019 are owned by AgentKit.Evaluation. Logs carry identities and bounded vocabularies only, never prompts, model output, tool data, rubrics, or exception messages.</remarks>
internal static partial class EvaluationLog
{
    /// <summary>Records that a validated plan began executing.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="planId">The plan.</param>
    /// <param name="planVersion">The plan version.</param>
    /// <param name="caseRuns">The number of case repetitions scheduled.</param>
    [LoggerMessage(EventId = 36000, Level = LogLevel.Information, Message = "Evaluation run {RunId} of plan {PlanId} version {PlanVersion} started with {CaseRuns} case repetitions.")]
    internal static partial void RunStarted(ILogger logger, EvaluationRunId runId, EvaluationPlanId planId, EvaluationPlanVersion planVersion, int caseRuns);

    /// <summary>Records the terminal state of one evaluation run.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for a completed run, Warning otherwise.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="planId">The plan.</param>
    /// <param name="status">The bounded status.</param>
    /// <param name="recorded">The number of recorded repetitions.</param>
    /// <param name="notStarted">The number of repetitions never scheduled.</param>
    [LoggerMessage(EventId = 36001, Message = "Evaluation run {RunId} of plan {PlanId} ended {Status} with {Recorded} recorded and {NotStarted} not started repetitions.")]
    internal static partial void RunCompleted(ILogger logger, LogLevel level, EvaluationRunId runId, EvaluationPlanId planId, string status, int recorded, int notStarted);

    /// <summary>Records that a plan was rejected before any effect.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="planId">The plan.</param>
    /// <param name="problems">The number of detected problems.</param>
    [LoggerMessage(EventId = 36002, Level = LogLevel.Warning, Message = "Evaluation run {RunId} of plan {PlanId} was rejected before any effect with {Problems} problems.")]
    internal static partial void PlanRejected(ILogger logger, EvaluationRunId runId, EvaluationPlanId planId, int problems);

    /// <summary>Records the terminal state of one case repetition.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for an evaluated repetition, Warning otherwise.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="disposition">The bounded disposition.</param>
    /// <param name="verdict">The bounded verdict.</param>
    [LoggerMessage(EventId = 36003, Message = "Evaluation run {RunId} case {CaseId} repetition {Repetition} ended {Disposition} with verdict {Verdict}.")]
    internal static partial void CaseCompleted(ILogger logger, LogLevel level, EvaluationRunId runId, EvaluationCaseId caseId, int repetition, string disposition, string verdict);

    /// <summary>Records the outcome of one evaluator invocation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="evaluatorKey">The configured evaluator key.</param>
    /// <param name="outcome">The bounded outcome name.</param>
    [LoggerMessage(EventId = 36004, Level = LogLevel.Debug, Message = "Evaluation run {RunId} case {CaseId} repetition {Repetition} evaluator {EvaluatorKey} concluded {Outcome}.")]
    internal static partial void EvaluatorConcluded(ILogger logger, EvaluationRunId runId, EvaluationCaseId caseId, int repetition, string evaluatorKey, string outcome);

    /// <summary>Records that an evaluator threw, by exception type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="evaluatorKey">The configured evaluator key.</param>
    /// <param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 36005, Level = LogLevel.Error, Message = "Evaluation run {RunId} case {CaseId} repetition {Repetition} evaluator {EvaluatorKey} failed with {ErrorType}.")]
    internal static partial void EvaluatorFaulted(ILogger logger, EvaluationRunId runId, EvaluationCaseId caseId, int repetition, string evaluatorKey, string errorType);

    /// <summary>Records that the engine threw while running a case, by exception type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 36006, Level = LogLevel.Error, Message = "Evaluation run {RunId} case {CaseId} repetition {Repetition} faulted in the engine with {ErrorType}.")]
    internal static partial void CaseFaulted(ILogger logger, EvaluationRunId runId, EvaluationCaseId caseId, int repetition, string errorType);

    /// <summary>Records that a result store did not acknowledge an append.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="failure">The bounded failure name.</param>
    [LoggerMessage(EventId = 36007, Level = LogLevel.Warning, Message = "Evaluation run {RunId} case {CaseId} repetition {Repetition} was not recorded by the result store: {Failure}.")]
    internal static partial void StoreAppendFailed(ILogger logger, EvaluationRunId runId, EvaluationCaseId caseId, int repetition, string failure);

    /// <summary>Records the terminal state of one report export.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for an exported report, Warning otherwise.</param>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="outcome">The bounded outcome name.</param>
    [LoggerMessage(EventId = 36008, Message = "Evaluation run {RunId} export to {ExporterKey} ended {Outcome}.")]
    internal static partial void ExportCompleted(ILogger logger, LogLevel level, EvaluationRunId runId, string exporterKey, string outcome);
}
