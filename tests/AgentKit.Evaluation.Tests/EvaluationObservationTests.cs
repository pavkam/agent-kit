// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationObservationTests
{
    [Fact]
    public void Safe_WhenTheObservationThrows_SwallowsTheFailure() =>
        Should.NotThrow(() => EvaluationObservation.Safe(static () => throw new InvalidOperationException("listener failure")));

    [Theory]
    [InlineData(EvaluationReportStatus.Completed, "completed")]
    [InlineData(EvaluationReportStatus.Cancelled, "cancelled")]
    [InlineData(EvaluationReportStatus.DeadlineExceeded, "deadline_exceeded")]
    [InlineData(EvaluationReportStatus.StoppedOnEvaluatorFailure, "stopped_on_evaluator_failure")]
    public void Name_WhenStatusIsDefined_ReturnsTheBoundedName(EvaluationReportStatus status, string expected) =>
        EvaluationObservation.Name(status).ShouldBe(expected);

    [Theory]
    [InlineData(EvaluationCaseDisposition.Evaluated, "evaluated")]
    [InlineData(EvaluationCaseDisposition.RunRejected, "run_rejected")]
    [InlineData(EvaluationCaseDisposition.Cancelled, "cancelled")]
    [InlineData(EvaluationCaseDisposition.TimedOut, "timed_out")]
    [InlineData(EvaluationCaseDisposition.Faulted, "faulted")]
    public void Name_WhenDispositionIsDefined_ReturnsTheBoundedName(EvaluationCaseDisposition disposition, string expected) =>
        EvaluationObservation.Name(disposition).ShouldBe(expected);

    [Theory]
    [InlineData(EvaluationVerdict.Passed, "passed")]
    [InlineData(EvaluationVerdict.Failed, "failed")]
    [InlineData(EvaluationVerdict.Inconclusive, "inconclusive")]
    [InlineData(EvaluationVerdict.NotEvaluated, "not_evaluated")]
    public void Name_WhenVerdictIsDefined_ReturnsTheBoundedName(EvaluationVerdict verdict, string expected) =>
        EvaluationObservation.Name(verdict).ShouldBe(expected);

    [Fact]
    public void Name_WhenAnEnumerationIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => EvaluationObservation.Name((EvaluationReportStatus) 9)).ParamName.ShouldBe("status");
        Should.Throw<ArgumentOutOfRangeException>(() => EvaluationObservation.Name((EvaluationCaseDisposition) 9)).ParamName.ShouldBe("disposition");
        Should.Throw<ArgumentOutOfRangeException>(() => EvaluationObservation.Name((EvaluationVerdict) 9)).ParamName.ShouldBe("verdict");
    }

    [Fact]
    public void Name_WhenExportResultIsGiven_ReturnsTheBoundedOutcome()
    {
        EvaluationObservation.Name(new EvaluationExported()).ShouldBe("exported");
        EvaluationObservation.Name(new EvaluationExportRejected(EvaluationExportFailureKind.Cancelled, "m")).ShouldBe("cancelled");
        EvaluationObservation.Name(new EvaluationExportRejected(EvaluationExportFailureKind.Faulted, "m")).ShouldBe("faulted");
        EvaluationObservation.Name(new EvaluationExportRejected(EvaluationExportFailureKind.Unavailable, "m")).ShouldBe("unavailable");
    }
}
