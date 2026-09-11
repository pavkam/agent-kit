// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolResultProjectionPolicyResolvedTests
{
    [Fact]
    public void Constructor_WhenSnapshotIsNull_RejectsExactParameter()
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyResolved(null!));

        // Assert
        exception.ParamName.ShouldBe("snapshot");
    }

    [Fact]
    public void Constructor_WhenSnapshotIsProvided_RetainsTheExactReferenceAndContent()
    {
        // Arrange
        var snapshot = ToolProjectionPolicyTestData.Snapshot();

        // Act
        var result = new ToolResultProjectionPolicyResolved(snapshot);

        // Assert
        result.Snapshot.ShouldBeSameAs(snapshot);
        result.Reference.ShouldBeSameAs(snapshot.Reference);
        (result with { }).ShouldBe(result);
        var reconstructed = new ToolResultProjectionPolicyResolved(ToolProjectionPolicyTestData.Snapshot());
        result.ShouldBe(reconstructed);
        result.GetHashCode().ShouldBe(reconstructed.GetHashCode());
        result.ShouldNotBe(new ToolResultProjectionPolicyResolved(ToolProjectionPolicyTestData.Snapshot(maximumBytes: 512)));
    }
}
