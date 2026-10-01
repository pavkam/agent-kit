// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationRunRecordTests
{
    [Fact]
    public void Constructor_WhenIdentitiesAreDefault_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationRunRecord(default, RunResultTestData.Session, "o", "s")).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationRunRecord(RunResultTestData.Run, default, "o", "s")).ParamName.ShouldBe("sessionId");
    }

    [Theory]
    [InlineData("", "s", "outcome")]
    [InlineData("o", " ", "settlement")]
    public void Constructor_WhenTextIsBlank_ThrowsWithTheParameterName(string outcome, string settlement, string parameter) =>
        Should.Throw<ArgumentException>(() => new EvaluationRunRecord(RunResultTestData.Run, RunResultTestData.Session, outcome, settlement)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesTypedIdentities()
    {
        var record = new EvaluationRunRecord(RunResultTestData.Run, RunResultTestData.Session, "succeeded", "completed");

        record.RunId.ShouldBe(RunResultTestData.Run);
        record.SessionId.ShouldBe(RunResultTestData.Session);
    }
}
