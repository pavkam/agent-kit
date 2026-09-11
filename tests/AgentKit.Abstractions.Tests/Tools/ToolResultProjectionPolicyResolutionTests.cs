// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolResultProjectionPolicyResolutionTests
{
    [Fact]
    public void Constructor_WhenForeignOutcomeIsCreated_RejectsTheUnsupportedFamily()
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentException>(() => new ForeignToolResultProjectionPolicyResolution());

        // Assert
        exception.ParamName.ShouldBe("resolution");
    }

    [Fact]
    public void CopyConstructor_WhenForeignOutcomeCopiesABuiltIn_RejectsTheUnsupportedFamily()
    {
        // Arrange
        var original = new ToolResultProjectionPolicyResolved(ToolProjectionPolicyTestData.Snapshot());

        // Act
        var exception = Should.Throw<ArgumentException>(() => new ForeignToolResultProjectionPolicyResolution(original));

        // Assert
        exception.ParamName.ShouldBe("resolution");
    }

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_RejectsExactParameter()
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignToolResultProjectionPolicyResolution(null!));

        // Assert
        exception.ParamName.ShouldBe("original");
    }
}
