// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using System.Collections.Immutable;

using SharpVision.Layout;

public sealed class WorkspaceConfigurationDialogTests
{
    private static readonly string Workspace = Path.Combine(Path.GetTempPath(), "coding-agent-tests", "workspace");
    private static readonly string Homebrew = Path.Combine(Path.GetTempPath(), "coding-agent-tests", "homebrew");
    private static readonly string Dotnet = Path.Combine(Path.GetTempPath(), "coding-agent-tests", "dotnet");

    [Fact]
    public void Constructor_WhenRootsAreDeclared_CreatesOneChoicePerRootWithTheSelectionLoaded()
    {
        // Arrange
        var roots = ImmutableArray.Create(Homebrew, Dotnet);

        // Act
        var dialog = new WorkspaceConfigurationDialog(Workspace, roots, [Dotnet], static _ => true);

        // Assert
        dialog.Header.ShouldContain("Configure Workspace");
        dialog.Width.ShouldBe(Length.Percent(90));
        dialog.CanMove.ShouldBeFalse();
        dialog.RootChoices.Select(static choice => choice.Text).ShouldBe(roots);
        dialog.RootChoices.Select(static choice => choice.IsChecked).ShouldBe([false, true]);
        dialog.RootChoices.ShouldAllBe(static choice => choice.Style == null);
        dialog.PathInput.Text.ShouldBeEmpty();
        dialog.HasSelectedResult.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenSelectedRootWasNotDeclaredByHost_StillListsItChecked()
    {
        // Act
        var dialog = new WorkspaceConfigurationDialog(Workspace, [Homebrew], [Dotnet], static _ => true);

        // Assert
        dialog.RootChoices.Select(static choice => choice.Text).ShouldBe([Homebrew, Dotnet]);
        dialog.RootChoices.Select(static choice => choice.IsChecked).ShouldBe([false, true]);
    }

    [Fact]
    public void Constructor_WhenNoRootsExist_LeavesTheFolderEntryUsable()
    {
        // Act
        var dialog = new WorkspaceConfigurationDialog(Workspace, [], [], static _ => true);

        // Assert
        dialog.RootChoices.ShouldBeEmpty();
        dialog.PathInput.IsReadOnly.ShouldBeFalse();
        dialog.PathInput.IsEnabled.ShouldBeTrue();
        dialog.AddButton.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void TryAddRoot_WhenPathIsValid_AppendsACheckedChoiceAndClearsTheEntry()
    {
        // Arrange
        var dialog = new WorkspaceConfigurationDialog(Workspace, [], [], static _ => true);
        dialog.PathInput.Text = Homebrew + Path.DirectorySeparatorChar;

        // Act
        var added = dialog.TryAddRoot();

        // Assert
        added.ShouldBeTrue();
        dialog.RootChoices.ShouldHaveSingleItem().Text.ShouldBe(Homebrew);
        dialog.RootChoices[0].IsChecked.ShouldBe(true);
        dialog.PathInput.Text.ShouldBeEmpty();
        dialog.ValidationText.ShouldContain("Added");
    }

    [Fact]
    public void TryAddRoot_WhenPathIsRejected_KeepsTheEntryAndShowsTheReason()
    {
        // Arrange
        var dialog = new WorkspaceConfigurationDialog(Workspace, [], [], static _ => false);
        dialog.PathInput.Text = Homebrew;

        // Act
        var added = dialog.TryAddRoot();

        // Assert
        added.ShouldBeFalse();
        dialog.RootChoices.ShouldBeEmpty();
        dialog.PathInput.Text.ShouldBe(Homebrew);
        dialog.ValidationText.ShouldContain("does not exist");
    }

    [Fact]
    public void TryAddRoot_WhenPathIsAlreadyListed_RejectsTheDuplicate()
    {
        // Arrange
        var dialog = new WorkspaceConfigurationDialog(Workspace, [Homebrew], [], static _ => true);
        dialog.PathInput.Text = Homebrew;

        // Act
        var added = dialog.TryAddRoot();

        // Assert
        added.ShouldBeFalse();
        dialog.RootChoices.Count.ShouldBe(1);
        dialog.ValidationText.ShouldContain("already listed");
    }

    [Fact]
    public void Constructor_WhenWorkspaceRootIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => new WorkspaceConfigurationDialog(" ", [], []));

    [Theory]
    [InlineData("/Users/alex/Development/repo", "/Users/alex", "~/Development/repo")]
    [InlineData("/Users/alex", "/Users/alex/", "~")]
    [InlineData("/Users/alexandra/repo", "/Users/alex", "/Users/alexandra/repo")]
    [InlineData("/opt/homebrew", null, "/opt/homebrew")]
    [InlineData("/opt/homebrew", "", "/opt/homebrew")]
    public void CompactPath_WhenHomePrefixMayApply_AbbreviatesOnlyWholeSegments(
        string path,
        string? home,
        string expected) =>
        WorkspaceConfigurationDialog.CompactPath(path, home).ShouldBe(expected);

    [Fact]
    public void CompactPath_WhenASegmentIsALongHexHash_KeepsOnlyItsPrefix()
    {
        var path = "/data/workspaces/d2d38616e31c60ddbb642366faeed658efa1df6b672db87f910edeb3ba63609f/sessions.db";

        WorkspaceConfigurationDialog.CompactPath(path, null).ShouldBe("/data/workspaces/d2d38616…/sessions.db");
    }

    [Fact]
    public void CompactPath_WhenPathIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => WorkspaceConfigurationDialog.CompactPath(null!, "/home"))
            .ParamName.ShouldBe("path");
}
