// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using System.Collections.Immutable;

using SharpVision.Layout;

public sealed class WorkspaceConfigurationDialogTests
{
    [Fact]
    public void Constructor_WhenRootsAreDeclared_CreatesOneChoicePerRootWithTheSelectionLoaded()
    {
        // Arrange
        var roots = ImmutableArray.Create("/opt/dotnet", "/opt/git");

        // Act
        var dialog = new WorkspaceConfigurationDialog("/workspace", roots, ["/opt/git"]);

        // Assert
        dialog.Header.ShouldContain("Configure Workspace");
        dialog.Width.ShouldBe(Length.Percent(90));
        dialog.CanMove.ShouldBeFalse();
        dialog.RootChoices.Select(static choice => choice.Text).ShouldBe(roots);
        dialog.RootChoices.Select(static choice => choice.IsChecked).ShouldBe([false, true]);
        dialog.RootChoices.ShouldAllBe(static choice => choice.Style == null);
        dialog.HasSelectedResult.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenWorkspaceRootIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => new WorkspaceConfigurationDialog(" ", [], []));
}
