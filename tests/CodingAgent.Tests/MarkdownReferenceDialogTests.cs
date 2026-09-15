// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using SharpVision.Controls.Document;
using SharpVision.Layout;
using SharpVision.Scrolling;

public sealed class MarkdownReferenceDialogTests
{
    [Theory]
    [InlineData("Keyboard Shortcuts")]
    [InlineData("Command Reference")]
    [InlineData("Available Tools")]
    public void Constructor_WhenCreated_UsesSelectableScrollableDocumentUnderADialogTitle(string title)
    {
        // Act
        var dialog = new MarkdownReferenceDialog(title, "# Heading\n\nBody.");

        // Assert
        dialog.Header.ShouldContain(title);
        dialog.Width.ShouldBe(Length.Percent(88));
        dialog.CanMove.ShouldBeFalse();
        dialog.CloseOnEscape.ShouldBeTrue();
        _ = dialog.Document.ShouldBeOfType<Document>();
        dialog.Document.IsTextSelectionEnabled.ShouldBeTrue();
        dialog.Document.IsFocusable.ShouldBeTrue();
        dialog.Document.ScrollBars.ShouldBe(ScrollBars.Vertical);
        dialog.Document.Blocks.ShouldNotBeEmpty();
        dialog.HasSelectedResult.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenTitleIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => new MarkdownReferenceDialog(" ", "body"));
}
