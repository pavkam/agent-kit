// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeOptionsTests
{
    [Fact]
    public void Constructor_WhenCreated_HasBoundedDefaultsAndNoJudgeModel()
    {
        var options = new ModelJudgeOptions();

        options.JudgeModel.ShouldBe(default);
        options.RepeatCount.ShouldBe(3);
        options.MaximumCalls.ShouldBe(300);
        options.MaximumTokens.ShouldBe(1_000_000);
        options.MaximumStandardDeviation.ShouldBe(0.25);
        options.MaximumCandidateCharacters.ShouldBe(16_000);
    }
}
