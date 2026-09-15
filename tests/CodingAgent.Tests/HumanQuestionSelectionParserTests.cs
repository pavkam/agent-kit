// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using System.Collections.Immutable;

using AgentKit;

/// <summary>Verifies numbered question answers preserve exact option identity and free-text rules.</summary>
public sealed class HumanQuestionSelectionParserTests
{
    [Fact]
    public void TryParse_WhenChoiceAndFreeTextAreValid_ReturnsExactSelection()
    {
        var options = Options();

        var parsed = HumanQuestionSelectionParser.TryParse(
            "2\nUse the safer validation seam.",
            options,
            allowsFreeText: true,
            out var selection);

        parsed.ShouldBeTrue();
        selection.OptionId.ShouldBe(options[1].Id);
        selection.FreeText.ShouldBe("Use the safer validation seam.");
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("3", true)]
    [InlineData("two", true)]
    [InlineData("1 unexpected", false)]
    public void TryParse_WhenChoiceViolatesPrompt_ReturnsFalse(string text, bool allowsFreeText) =>
        HumanQuestionSelectionParser.TryParse(text, Options(), allowsFreeText, out _).ShouldBeFalse();

    private static ImmutableArray<HumanQuestionOption> Options() =>
    [
        new HumanQuestionOption(new QuestionOptionId("small"), "Small", "Use the narrow change."),
        new HumanQuestionOption(new QuestionOptionId("safe"), "Safe", "Include validation."),
    ];
}
