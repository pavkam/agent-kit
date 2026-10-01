// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationCaseExecutionTests
{
    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new EvaluationCaseExecution(null!, EvaluationTestData.SessionProfile))
            .ParamName.ShouldBe("identity");

    [Fact]
    public void Constructor_WhenSessionProfileIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new EvaluationCaseExecution(EvaluationTestData.Identity(), default))
            .ParamName.ShouldBe("sessionProfile");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_CarriesIdentityAndProfileUnchanged()
    {
        var identity = EvaluationTestData.Identity();

        var execution = new EvaluationCaseExecution(identity, EvaluationTestData.SessionProfile);

        execution.Identity.ShouldBeSameAs(identity);
        execution.SessionProfile.ShouldBe(EvaluationTestData.SessionProfile);
    }
}
