// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeFailedTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelJudgeFailed((ModelJudgeFailureKind) 9, "m")).ParamName.ShouldBe("kind");
        Should.Throw<ArgumentException>(() => new ModelJudgeFailed(ModelJudgeFailureKind.Unavailable, " ")).ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var failed = new ModelJudgeFailed(ModelJudgeFailureKind.Incomplete, "stopped");

        (failed.Kind, failed.SafeMessage).ShouldBe((ModelJudgeFailureKind.Incomplete, "stopped"));
    }
}
