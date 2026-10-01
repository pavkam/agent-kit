// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeRequestTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        var context = EvaluationTestData.Context();

        Should.Throw<ArgumentNullException>(() => new ModelJudgeRequest(null!, new ModelAlias("j"), "i", "c", 1)).ParamName.ShouldBe("context");
        Should.Throw<ArgumentException>(() => new ModelJudgeRequest(context, default, "i", "c", 1)).ParamName.ShouldBe("model");
        Should.Throw<ArgumentException>(() => new ModelJudgeRequest(context, new ModelAlias("j"), " ", "c", 1)).ParamName.ShouldBe("instructions");
        Should.Throw<ArgumentNullException>(() => new ModelJudgeRequest(context, new ModelAlias("j"), "i", null!, 1)).ParamName.ShouldBe("candidate");
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelJudgeRequest(context, new ModelAlias("j"), "i", "c", 0)).ParamName.ShouldBe("sample");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var context = EvaluationTestData.Context();

        var request = new ModelJudgeRequest(context, new ModelAlias("j"), "instructions", string.Empty, 2);

        (request.Model, request.Instructions, request.Candidate, request.Sample).ShouldBe((new ModelAlias("j"), "instructions", string.Empty, 2));
        request.Context.ShouldBeSameAs(context);
    }
}
