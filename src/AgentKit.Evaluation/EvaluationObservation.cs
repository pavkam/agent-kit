// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Keeps evaluation instrumentation observational: no listener, logger, or metric failure changes a result.</summary>
internal static class EvaluationObservation
{
    /// <summary>Runs one observation and swallows any failure so instrumentation never changes a semantic result.</summary>
    /// <param name="observation">The observation to run.</param>
    internal static void Safe(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Observation never changes the semantic result.
        }
    }

    /// <summary>Gets the stable bounded name of a report status.</summary>
    /// <param name="status">The defined status.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    internal static string Name(EvaluationReportStatus status) => status switch
    {
        EvaluationReportStatus.Completed => "completed",
        EvaluationReportStatus.Cancelled => "cancelled",
        EvaluationReportStatus.DeadlineExceeded => "deadline_exceeded",
        EvaluationReportStatus.StoppedOnEvaluatorFailure => "stopped_on_evaluator_failure",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The evaluation report status is undefined."),
    };

    /// <summary>Gets the stable bounded name of a case disposition.</summary>
    /// <param name="disposition">The defined disposition.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="disposition"/> is undefined.</exception>
    internal static string Name(EvaluationCaseDisposition disposition) => disposition switch
    {
        EvaluationCaseDisposition.Evaluated => "evaluated",
        EvaluationCaseDisposition.RunRejected => "run_rejected",
        EvaluationCaseDisposition.Cancelled => "cancelled",
        EvaluationCaseDisposition.TimedOut => "timed_out",
        EvaluationCaseDisposition.Faulted => "faulted",
        _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "The evaluation case disposition is undefined."),
    };

    /// <summary>Gets the stable bounded name of a verdict.</summary>
    /// <param name="verdict">The defined verdict.</param>
    /// <returns>The bounded name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="verdict"/> is undefined.</exception>
    internal static string Name(EvaluationVerdict verdict) => verdict switch
    {
        EvaluationVerdict.Passed => "passed",
        EvaluationVerdict.Failed => "failed",
        EvaluationVerdict.Inconclusive => "inconclusive",
        EvaluationVerdict.NotEvaluated => "not_evaluated",
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "The evaluation verdict is undefined."),
    };

    /// <summary>Gets the stable bounded name of an export outcome.</summary>
    /// <param name="result">The typed export answer.</param>
    /// <returns>The bounded name.</returns>
    internal static string Name(EvaluationExportResult result) => result switch
    {
        EvaluationExported => "exported",
        EvaluationExportRejected { Kind: EvaluationExportFailureKind.Cancelled } => "cancelled",
        EvaluationExportRejected { Kind: EvaluationExportFailureKind.Faulted } => "faulted",
        EvaluationExportRejected => "unavailable",
        _ => "unknown",
    };
}
