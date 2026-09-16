// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies HumanQuestionAnswered behavior and contracts.</summary>
public sealed class HumanQuestionAnsweredTests
{
    [Fact]
    public void Constructor_WhenAnswerIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionAnswered(InputTestData.QuestionId, null!)).ParamName.ShouldBe("answer");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var answer = InputTestData.Answer();
        var answered = new HumanQuestionAnswered(InputTestData.QuestionId, answer);
        answered.QuestionId.ShouldBe(InputTestData.QuestionId);
        answered.Answer.ShouldBe(answer);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new HumanQuestionAnswered(InputTestData.QuestionId, InputTestData.Answer());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
