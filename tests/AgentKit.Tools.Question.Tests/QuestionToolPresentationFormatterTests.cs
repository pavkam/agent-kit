// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Question.Tests;

/// <summary>Verifies readable bounded question and answer presentation without transport JSON.</summary>
public sealed class QuestionToolPresentationFormatterTests
{
    [Fact]
    public async Task FormatAsync_WhenQuestionIsValid_RendersPromptOptionsFreeTextAndDeadline()
    {
        const string json = /*lang=json,strict*/ """
            {
              "question": "Choose a deployment strategy.",
              "options": [
                { "id": "rolling", "label": "Rolling", "description": "Replace instances gradually." },
                { "id": "blue-green", "label": "Blue-green", "description": "Switch traffic after validation." }
              ],
              "allow_free_text": true,
              "timeout_seconds": 90
            }
            """;

        var presentation = await FormatCallAsync(json, new ToolPresentationBounds());

        var text = presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text;
        text.ShouldContain("Choose a deployment strategy.");
        text.ShouldContain("1. Rolling [rolling]");
        text.ShouldContain("2. Blue-green [blue-green]");
        text.ShouldContain("Free-text answer: allowed");
        text.ShouldContain("Response deadline: 90 seconds");
        text.ShouldNotContain("\"options\"");
    }

    [Fact]
    public async Task FormatAsync_WhenAnswerIsValid_RendersSelectionAndFreeTextWithoutJson()
    {
        const string projection = /*lang=json,strict*/ """
            {"question_id":"q","selected_option_id":"rolling","selected_option_label":"Rolling","free_text":"Keep one spare.","instruction_authority":false}
            """;

        var presentation = await FormatResultAsync(
            Success([new TextPart(projection, TextSemantics.Code, ExtensionData.Empty)]),
            new ToolPresentationBounds());

        var text = presentation.ShouldNotBeNull().Parts.ShouldHaveSingleItem().Text;
        text.ShouldBe("Selected answer: Rolling [rolling]\nFree text: Keep one spare.");
        text.ShouldNotContain("selected_option_id");
    }

    [Fact]
    public async Task FormatAsync_WhenCallIsMalformed_PreservesSafeFailureWithoutRawJson()
    {
        const string json = /*lang=json,strict*/ "{\"question\":\"Secret raw transport\",\"options\":[]}";

        var presentation = await FormatCallAsync(json, new ToolPresentationBounds());

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Fallback);
        presentation.Parts.ShouldHaveSingleItem().Text.ShouldContain("malformed");
        presentation.Parts[0].Text.ShouldNotContain("Secret raw transport");
    }

    [Fact]
    public async Task FormatAsync_WhenResultFailed_RendersBoundedFailureEvidence()
    {
        var outcome = new ToolCallOutcome(
            ToolCallOutcomeKind.Failed,
            ToolTerminalStatus.TimedOut,
            SideEffectCertainty.Unknown,
            false,
            "No answer arrived before the question deadline.",
            ExtensionData.Empty);

        var presentation = await FormatResultAsync(
            Result(outcome, []),
            new ToolPresentationBounds(maximumOutputCharacters: 24));

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.Parts.ShouldHaveSingleItem().Text.Length.ShouldBe(24);
        presentation.OmittedCharacters.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task FormatAsync_WhenInputExceedsBound_ReturnsSafeTruncatedEvidence()
    {
        const string json = /*lang=json,strict*/ """
            {"question":"A long prompt","options":[{"id":"a","label":"A","description":"First"},{"id":"b","label":"B","description":"Second"}]}
            """;

        var presentation = await FormatCallAsync(
            json,
            new ToolPresentationBounds(maximumInputBytes: 8));

        presentation.ShouldNotBeNull().Disposition.ShouldBe(ToolPresentationDisposition.Truncated);
        presentation.OmittedCharacters.ShouldBeGreaterThan(0);
        presentation.Parts[0].Text.ShouldNotContain("A long prompt");
    }

    private static ValueTask<ToolPresentation?> FormatCallAsync(string json, ToolPresentationBounds bounds)
    {
        using var document = JsonDocument.Parse(json);
        var formatter = new QuestionToolPresentationFormatter();
        var call = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(QuestionTool.Id, null, "question"),
            document.RootElement.Clone(),
            null,
            ExtensionData.Empty);
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolCallPresentationSource(call), bounds),
            TestContext.Current.CancellationToken);
    }

    private static ValueTask<ToolPresentation?> FormatResultAsync(ToolResultPart result, ToolPresentationBounds bounds)
    {
        var formatter = new QuestionToolPresentationFormatter();
        return formatter.FormatAsync(
            new ToolPresentationRequest(formatter.Descriptor, new ToolResultPresentationSource(result), bounds),
            TestContext.Current.CancellationToken);
    }

    private static ToolResultPart Success(ImmutableArray<ContentPart> content) => Result(
        new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty),
        content);

    private static ToolResultPart Result(ToolCallOutcome outcome, ImmutableArray<ContentPart> content) => new(
        new ToolCallId(Guid.NewGuid()),
        new ToolReference(QuestionTool.Id, null, "question"),
        outcome,
        content,
        ExtensionData.Empty);
}
