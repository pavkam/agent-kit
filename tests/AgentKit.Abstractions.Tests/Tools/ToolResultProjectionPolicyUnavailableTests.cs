// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolResultProjectionPolicyUnavailableTests
{
    [Fact]
    public void Constructor_WhenReferenceIsNull_RejectsExactParameter()
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyUnavailable(null!));

        // Assert
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void Constructor_WhenReferenceIsProvided_RetainsItAcrossReconstructionAndCopy()
    {
        // Arrange
        var reference = ToolProjectionPolicyTestData.Snapshot().Reference;

        // Act
        var result = new ToolResultProjectionPolicyUnavailable(reference);

        // Assert
        result.Reference.ShouldBeSameAs(reference);
        (result with { }).ShouldBe(result);
        var reconstructed = new ToolResultProjectionPolicyUnavailable(ToolProjectionPolicyTestData.Snapshot().Reference);
        result.ShouldBe(reconstructed);
        result.GetHashCode().ShouldBe(reconstructed.GetHashCode());
        result.ShouldNotBe(new ToolResultProjectionPolicyUnavailable(ToolProjectionPolicyTestData.Snapshot(version: 2).Reference));
    }
}
