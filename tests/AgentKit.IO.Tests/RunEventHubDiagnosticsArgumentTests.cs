// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class RunEventHubDiagnosticsArgumentTests
{
    [Theory]
    [InlineData("operation")]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("runId")]
    public void Constructor_WhenObservationCoordinatesAreInvalid_RejectsExactArgument(string parameter)
    {
        var basis = RunEventHubTests.Event(1);
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventHubObservation(
            parameter == "operation" ? (RunEventHubOperation) (-1) : RunEventHubOperation.Publish,
            parameter == "agentId" ? default : basis.AgentId,
            parameter == "sessionId" ? default : basis.SessionId,
            parameter == "runId" ? default : basis.RunId,
            TimeProvider.System, NullLogger.Instance));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData("timeProvider")]
    [InlineData("logger")]
    public void Constructor_WhenObservationCollaboratorIsNull_RejectsExactArgument(string parameter)
    {
        var basis = RunEventHubTests.Event(1);
        var exception = Should.Throw<ArgumentNullException>(() => new RunEventHubObservation(
            RunEventHubOperation.Publish, basis.AgentId, basis.SessionId, basis.RunId,
            parameter == "timeProvider" ? null! : TimeProvider.System,
            parameter == "logger" ? null! : NullLogger.Instance));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Finish_WhenOutcomeIsUndefined_RejectsExactArgument()
    {
        var basis = RunEventHubTests.Event(1);
        using var observation = new RunEventHubObservation(RunEventHubOperation.Publish,
            basis.AgentId, basis.SessionId, basis.RunId, TimeProvider.System, NullLogger.Instance);
        Should.Throw<ArgumentOutOfRangeException>(() => observation.Finish((RunEventHubOutcome) (-1))).ParamName.ShouldBe("outcome");
        observation.Finish(RunEventHubOutcome.Succeeded);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("outcome")]
    [InlineData("elapsed")]
    public void Record_WhenMetricDimensionsAreInvalid_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => RunEventHubMetrics.Record(
            parameter == "operation" ? (RunEventHubOperation) (-1) : RunEventHubOperation.Publish,
            parameter == "outcome" ? (RunEventHubOutcome) (-1) : RunEventHubOutcome.Succeeded,
            parameter == "elapsed" ? TimeSpan.FromTicks(-1) : null));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }
}
