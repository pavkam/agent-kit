// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

/// <summary>Verifies HumanQuestionOption behavior and contracts.</summary>
public sealed class HumanQuestionOptionTests
{
    [Fact]
    public void Constructor_WhenLabelIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionOption(new QuestionOptionId("yes"), " ", "Description.")).ParamName.ShouldBe("label");

    [Fact]
    public void Constructor_WhenDescriptionIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new HumanQuestionOption(new QuestionOptionId("yes"), "Yes", " ")).ParamName.ShouldBe("description");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var option = new HumanQuestionOption(new QuestionOptionId("yes"), "Yes", "Proceed.");
        option.Id.ShouldBe(new QuestionOptionId("yes"));
        option.Label.ShouldBe("Yes");
        option.Description.ShouldBe("Proceed.");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new HumanQuestionOption(new QuestionOptionId("yes"), "Yes", "Proceed.");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
