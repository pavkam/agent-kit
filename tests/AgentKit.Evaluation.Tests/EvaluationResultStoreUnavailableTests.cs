// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultStoreUnavailableTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreBlank_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentException>(() => new EvaluationResultStoreUnavailable(default, "m")).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => new EvaluationResultStoreUnavailable(new EvaluationResultStoreKey("s"), " ")).ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesThem()
    {
        var outcome = new EvaluationResultStoreUnavailable(new EvaluationResultStoreKey("s"), "none registered");

        (outcome.Key, outcome.SafeMessage).ShouldBe((new EvaluationResultStoreKey("s"), "none registered"));
    }
}
