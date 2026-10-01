// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeCompletedTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ModelJudgeCompleted(null!, "p", "m", null, null)).ParamName.ShouldBe("text");
        Should.Throw<ArgumentException>(() => new ModelJudgeCompleted("t", " ", "m", null, null)).ParamName.ShouldBe("provider");
        Should.Throw<ArgumentException>(() => new ModelJudgeCompleted("t", "p", "", null, null)).ParamName.ShouldBe("resolvedModel");
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelJudgeCompleted("t", "p", "m", -1, null)).ParamName.ShouldBe("inputTokens");
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelJudgeCompleted("t", "p", "m", null, -1)).ParamName.ShouldBe("outputTokens");
    }

    [Fact]
    public void Constructor_WhenTextIsEmptyAndUsageUnknown_IsValid()
    {
        var completed = new ModelJudgeCompleted(string.Empty, "p", "m", null, null);

        completed.Text.ShouldBeEmpty();
        completed.InputTokens.ShouldBeNull();
        completed.OutputTokens.ShouldBeNull();
    }
}
