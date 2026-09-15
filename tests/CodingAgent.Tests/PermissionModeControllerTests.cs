// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

/// <summary>Verifies the state transitions shared by the visible permission selector and security policy.</summary>
public sealed class PermissionModeControllerTests
{
    [Fact]
    public void Mode_WhenConstructed_DefaultsToAskForChanges()
    {
        // Arrange / Act
        var controller = new PermissionModeController();

        // Assert
        controller.Mode.ShouldBe(PermissionMode.AskForChanges);
        controller.Label().ShouldBe("Ask before changes");
    }

    [Theory]
    [InlineData((int) PermissionMode.ReadOnly, "Read-only")]
    [InlineData((int) PermissionMode.AutoApproveWorkspaceEdits, "Auto-approve workspace edits")]
    [InlineData((int) PermissionMode.AskForChanges, "Ask before changes")]
    public void Set_WhenModeIsSupported_ChangesModeAndLabel(int modeValue, string expectedLabel)
    {
        // Arrange
        var controller = new PermissionModeController();

        // Act
        var mode = (PermissionMode) modeValue;
        controller.Set(mode);

        // Assert
        controller.Mode.ShouldBe(mode);
        controller.Label().ShouldBe(expectedLabel);
    }

    [Fact]
    public void Set_WhenModeIsUnknown_ThrowsArgumentOutOfRangeExceptionWithoutChangingMode()
    {
        // Arrange
        var controller = new PermissionModeController();

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => controller.Set((PermissionMode) 99));

        // Assert
        exception.ParamName.ShouldBe("mode");
        controller.Mode.ShouldBe(PermissionMode.AskForChanges);
    }
}
