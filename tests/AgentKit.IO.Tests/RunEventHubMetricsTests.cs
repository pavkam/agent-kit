// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventHubMetricsTests
{
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
