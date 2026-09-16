// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies HumanQuestionAnswer behavior and contracts.</summary>
public sealed class HumanQuestionAnswerTests
{
    [Fact]
    public void Constructor_WhenRespondentIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new HumanQuestionAnswer(new QuestionOptionId("yes"), null, null!, DateTimeOffset.UnixEpoch)).ParamName.ShouldBe("respondent");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var identity = InputTestData.Identity();
        var answer = new HumanQuestionAnswer(new QuestionOptionId("yes"), "free text", identity, DateTimeOffset.UnixEpoch);
        answer.SelectedOptionId.ShouldBe(new QuestionOptionId("yes"));
        answer.FreeText.ShouldBe("free text");
        answer.Respondent.ShouldBe(identity);
        answer.AnsweredAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = InputTestData.Answer();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
