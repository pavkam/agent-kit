// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpCatalogSnapshot"/>.</summary>
public sealed class McpCatalogSnapshotTests
{
    [Fact]
    public void Constructor_WhenVersionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpCatalogSnapshot(
            new McpSessionId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new McpCatalogVersion(0),
            [],
            [],
            [],
            new McpCapabilitySet(tools: true)));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenSnapshotIsValid_PreservesCollections()
    {
        var snapshot = new McpCatalogSnapshot(
            new McpSessionId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new McpCatalogVersion(1),
            [],
            [new McpResourceDescriptor("file:///tmp/readme.md")],
            [new McpPromptDescriptor("summarize")],
            new McpCapabilitySet(tools: true, resources: true, prompts: true));

        _ = snapshot.Resources.ShouldHaveSingleItem();
        _ = snapshot.Prompts.ShouldHaveSingleItem();
        snapshot.Capabilities.Tools.ShouldBeTrue();
    }
}
