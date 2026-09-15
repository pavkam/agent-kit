// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using System.Text;

using AgentKit;
using AgentKit.Conversations;

using SharpVision.Controls.Document;
using SharpVision.Controls.Layout;
using SharpVision.Controls.SyntaxHighlighting;
using SharpVision.Documents.Markdown;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Scrolling;
using SharpVision.Styling;
using SharpVision.Terminal.Input;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Verifies transcript composition keeps row selection absent while semantic text remains selectable.</summary>
public sealed class ChatScreenTests
{
    [Fact]
    public void CreateCommandPalette_WhenConstructed_UsesResponsiveNativePalettePresentation()
    {
        var palette = ChatScreen.CreateCommandPalette();

        palette.Width.ShouldBe(Length.Percent(88));
        palette.DropDownHeight.ShouldBe(Length.Percent(58));
        palette.RowHeight.ShouldBe(Length.Auto);
        palette.Visibility.ShouldBe(Visibility.Collapsed);
        _ = palette.StartAffix.ShouldNotBeNull();
        palette.PopupChrome.Border.ShouldNotBeNull().GlyphStyle.ShouldBe(BorderGlyphStyle.Paired);
        palette.PopupChrome.Shadow.ShouldNotBeNull().IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void BuildCommandPaletteItems_WhenConstructed_ProvidesBroadCatalogAndExactCurrentMode()
    {
        var items = ChatScreen.BuildCommandPaletteItems(busy: true, PermissionMode.ReadOnly);

        items.Length.ShouldBe(17);
        items.Select(static item => item.Group).Distinct().ShouldBe(["Session", "View", "Agent", "Permissions", "Help", "Application"], ignoreOrder: true);
        items.Single(static item => item.Badge == "CURRENT").Id.ShouldBe("permissions.readonly");
        items.Single(static item => item.Id == "agent.stop").Badge.ShouldBe("RUNNING");
    }

    [Fact]
    public void MatchCommandPaletteItems_WhenQueryUsesKeywords_RequiresEveryWord()
    {
        var items = ChatScreen.BuildCommandPaletteItems(busy: false, PermissionMode.AskForChanges);

        var matches = ChatScreen.MatchCommandPaletteItems(items, "durable history");

        matches.ShouldHaveSingleItem().Id.ShouldBe("session.recent");
    }

    [Fact]
    public void MatchCommandPaletteItems_WhenTitleMatchesDescriptionElsewhere_RanksTitleFirst()
    {
        var items = ChatScreen.BuildCommandPaletteItems(busy: false, PermissionMode.AskForChanges);

        var matches = ChatScreen.MatchCommandPaletteItems(items, "workspace");

        matches[0].Id.ShouldBe("info.workspace");
    }

    [Fact]
    public void ModelMenuLabel_WhenConfigurationUsesCatalogModel_ShowsShortName()
    {
        var configuration = CodingAgentConfiguration.CreateDefault() with { ModelId = "gpt-6-astra" };

        ChatScreen.ModelMenuLabel(configuration).ShouldBe("&Model · Astra");
    }

    [Fact]
    public void ModelStatusLabel_WhenConfigurationProvided_ShowsProductNameAndEffort()
    {
        var configuration = CodingAgentConfiguration.CreateDefault() with
        {
            ModelId = "gpt-5.6-sol",
            ReasoningEffort = LlmReasoningEffort.High,
        };

        ChatScreen.ModelStatusLabel(configuration).ShouldBe("Sol · High");
    }

    [Fact]
    public void CreateStatusSegment_WhenValueProvided_UsesSpacedPipeSeparator()
    {
        var value = new Text("value");

        var segment = ChatScreen.CreateStatusSegment(value);

        segment.Spacing.ShouldBe(0);
        segment.Children[0].ShouldBeOfType<Text>().Content.ShouldBe(" | ");
        segment.Children[1].ShouldBeSameAs(value);
    }

    [Theory]
    [InlineData(LlmReasoningEffort.Low, "&Effort · Low")]
    [InlineData(LlmReasoningEffort.Medium, "&Effort · Medium")]
    [InlineData(LlmReasoningEffort.High, "&Effort · High")]
    [InlineData(LlmReasoningEffort.ExtraHigh, "&Effort · Extra high")]
    [InlineData(LlmReasoningEffort.None, "&Effort · Off")]
    public void ReasoningMenuLabel_WhenEffortIsDefined_ShowsCurrentValue(
        LlmReasoningEffort effort,
        string expected) =>
        ChatScreen.ReasoningMenuLabel(effort).ShouldBe(expected);

    [Theory]
    [InlineData('k', Modifiers.Control, true)]
    [InlineData('p', Modifiers.Control | Modifiers.Shift, true)]
    [InlineData('p', Modifiers.Control, false)]
    [InlineData('k', Modifiers.None, false)]
    public void IsCommandPaletteChord_WhenKeyUsesSupportedGesture_ReturnsExpectedDecision(
        char character,
        Modifiers modifiers,
        bool expected)
    {
        var key = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: character,
            modifiers,
            KeyAction.Press));

        ChatScreen.IsCommandPaletteChord(key).ShouldBe(expected);
    }

    [Fact]
    public void CreateTranscript_WhenConstructed_UsesRetainedScrollingStackWithoutSelectionOwnership()
    {
        var transcript = ChatScreen.CreateTranscript();

        _ = transcript.ShouldBeOfType<Stack>();
        transcript.Orientation.ShouldBe(Orientation.Vertical);
        transcript.AutoScroll.ShouldBeTrue();
        transcript.ScrollBars.ShouldBe(ScrollBars.Vertical);
        transcript.IsTextSelectionEnabled.ShouldBeFalse();
        transcript.IsFocusable.ShouldBeFalse();
    }

    [Fact]
    public void CreateComposer_WhenTextGainsAndLosesExplicitLines_RetainsIntrinsicGrowthAndContent()
    {
        var composer = ChatScreen.CreateComposer();
        ChatScreen.UpdateComposerMaximumRows(composer, 80);
        composer.Text = "one\ntwo\nthree";

        composer.Height.ShouldBe(Length.Auto);
        composer.Text.ShouldBe("one\ntwo\nthree");
        composer.AcceptsReturn.ShouldBeTrue();

        composer.Text = "";
        composer.Text.ShouldBeEmpty();
        composer.MinHeight.ShouldBe(Length.Cells(3));
    }

    [Fact]
    public void CreateComposer_WhenTextExceedsFiveRows_CapsAtFiveContentRows()
    {
        var composer = ChatScreen.CreateComposer();
        ChatScreen.UpdateComposerMaximumRows(composer, 80);
        composer.MaxHeight.ShouldBe(Length.Cells(7));
        ChatScreen.ComposerMaximumRows(80).ShouldBe(5);
    }

    [Fact]
    public void UpdateComposerMaximumRows_WhenTerminalIsNarrow_AppliesTenPercentContentRowCap()
    {
        var composer = ChatScreen.CreateComposer();
        ChatScreen.UpdateComposerMaximumRows(composer, 30);
        composer.MaxHeight.ShouldBe(Length.Cells(5));
        ChatScreen.ComposerMaximumRows(30).ShouldBe(3);
    }

    [Fact]
    public void CreateComposer_WhenWidthChanges_ReflowsWrappedTextWithinCurrentCap()
    {
        var composer = ChatScreen.CreateComposer();
        ChatScreen.UpdateComposerMaximumRows(composer, 80);
        composer.WordWrap.ShouldBeTrue();
        composer.MaxHeight.ShouldBe(Length.Cells(7));

        ChatScreen.UpdateComposerMaximumRows(composer, 30);
        composer.MaxHeight.ShouldBe(Length.Cells(5));
    }

    [Theory]
    [InlineData(Modifiers.None, true)]
    [InlineData(Modifiers.CapsLock, true)]
    [InlineData(Modifiers.NumLock, true)]
    [InlineData(Modifiers.Shift, false)]
    [InlineData(Modifiers.Shift | Modifiers.CapsLock, false)]
    [InlineData(Modifiers.Control, false)]
    [InlineData(Modifiers.Alt, false)]
    public void IsComposerSubmitKey_WhenEnterUsesModifiers_ReturnsExpectedDecision(
        Modifiers modifiers,
        bool expected) =>
        ChatScreen.IsComposerSubmitKey(Code.Enter, modifiers).ShouldBe(expected);

    [Fact]
    public void IsComposerSubmitKey_WhenKeyIsNotEnter_ReturnsFalse() =>
        ChatScreen.IsComposerSubmitKey(Code.Tab, Modifiers.None).ShouldBeFalse();

    [Fact]
    public void BuildEntryView_WhenAssistantProseCreated_OmitsRoleHeadingAndKeepsBodySelectable()
    {
        var screen = new ChatScreen("/workspace");

        var chrome = screen.BuildEntryView(new ChatEntry(ChatEntryKind.Assistant, "", "Selectable prose"));

        chrome.IsTextSelectionEnabled.ShouldBeFalse();
        chrome.IsFocusable.ShouldBeFalse();
        var document = chrome.Children.ShouldHaveSingleItem().ShouldBeOfType<Document>();
        document.IsTextSelectionEnabled.ShouldBeTrue();
        document.IsFocusable.ShouldBeTrue();
        document.VerticalAlignment.ShouldBe(VerticalAlignment.Top);
        document.Height.ShouldBe(Length.Auto);
        document.Blocks.ShouldNotContain(static block => block is DocumentBlockControl);
        document.SelectAllText();
        document.CopySelectedText().ShouldContain("Selectable prose");
    }

    [Fact]
    public void BuildEntryView_WhenToolPresentationContainsCode_UsesNaturalHeightDocumentBlocks()
    {
        var screen = new ChatScreen("/workspace");
        var presentation = new ToolPresentation(
            [
                new ToolPresentationPart(ToolPresentationPartKind.Text, "2 matches"),
                new ToolPresentationPart(ToolPresentationPartKind.Code, "one.cs\ntwo.cs", "text"),
            ],
            ToolPresentationDisposition.Formatted,
            0);

        var chrome = screen.BuildEntryView(new ChatEntry(
            ChatEntryKind.ToolResultSuccess,
            "Glob complete",
            string.Empty,
            Presentation: presentation));

        var document = chrome.Children.ShouldHaveSingleItem().ShouldBeOfType<Document>();
        _ = document.Blocks.OfType<DocumentParagraph>().ShouldHaveSingleItem();
        document.Blocks.OfType<DocumentCodeBlock>().ShouldHaveSingleItem().Text.ShouldBe("one.cs\ntwo.cs");
        document.Blocks.OfType<DocumentBlockControl>()
            .Select(static block => block.Control)
            .ShouldNotContain(static control => control is CodeView);
    }

    [Fact]
    public void ClearTranscriptSelections_WhenFinalClearRebuildsSource_ClearsSnapshotWithoutEnumerationFailure()
    {
        var first = new Document();
        var second = new Document();
        _ = first.Load("first", new MarkdownDocumentReader());
        _ = second.Load("second", new MarkdownDocumentReader());
        first.SelectAllText();
        second.SelectAllText();
        List<Document> documents = [first, second];
        first.TextSelectionChanged += (_, e) =>
        {
            if (e.Selection.IsEmpty)
            {
                documents.Clear();
            }
        };

        ChatScreen.ClearTranscriptSelections(documents);

        first.CopySelectedText().ShouldBeEmpty();
        second.CopySelectedText().ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadCompleteHistoryAsync_WhenMultiplePagesAdvance_ReadsThroughCompletePage()
    {
        var conversation = new PagingConversationSession(cursor => cursor.Value == 0
            ? new ConversationHistoryPage([], new SessionSequence(3), complete: false)
            : new ConversationHistoryPage([], new SessionSequence(6), complete: true));

        var messages = await ChatScreen.ReadCompleteHistoryAsync(
            conversation,
            TestContext.Current.CancellationToken);

        messages.ShouldBeEmpty();
        conversation.Cursors.ShouldBe([new SessionSequence(0), new SessionSequence(3)]);
    }

    [Fact]
    public async Task ReadCompleteHistoryAsync_WhenCursorStalls_RejectsIncompleteHydration()
    {
        var conversation = new PagingConversationSession(cursor =>
            new ConversationHistoryPage([], cursor, complete: false));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ChatScreen.ReadCompleteHistoryAsync(conversation, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("did not advance");
    }
}
