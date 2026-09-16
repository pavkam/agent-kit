// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies McpClientToolCatalogSnapshot behavior and contracts.</summary>
public sealed class McpClientToolCatalogSnapshotTests
{
    [Fact]
    public void CatalogSnapshot_WhenValuesAreValid_CapturesImmutableState()
    {
        var tools = ImmutableArray.Create(new McpRemoteToolDescriptor(new McpToolName("weather.get"), new ToolVersion("2.1")));
        var snapshot = new McpClientToolCatalogSnapshot(new McpCatalogVersion(4), McpProtocolVersions.July2026, tools);
        snapshot.Version.ShouldBe(new McpCatalogVersion(4));
        snapshot.ProtocolVersion.ShouldBe(McpProtocolVersions.July2026);
        snapshot.Tools.ShouldBe(tools);
        (snapshot with { }).ShouldBe(snapshot);
        snapshot.ToString().ShouldContain(nameof(McpClientToolCatalogSnapshot));
    }

    [Fact]
    public void CatalogSnapshot_WhenToolListIsEmpty_AllowsPublishedEmptyView()
    {
        var snapshot = new McpClientToolCatalogSnapshot(new McpCatalogVersion(1), McpProtocolVersions.November2025, []);
        snapshot.Tools.ShouldBeEmpty();
    }

    [Fact]
    public void CatalogSnapshot_WhenCatalogVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpClientToolCatalogSnapshot(default, McpProtocolVersions.July2026, []));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void CatalogSnapshot_WhenProtocolVersionIsUninitialized_ThrowsForProtocolVersion()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpClientToolCatalogSnapshot(new McpCatalogVersion(1), default, []));
        exception.ParamName.ShouldBe("protocolVersion");
    }

    [Fact]
    public void CatalogSnapshot_WhenToolsAreDefault_ThrowsForTools()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new McpClientToolCatalogSnapshot(new McpCatalogVersion(1), McpProtocolVersions.July2026, default));
        exception.ParamName.ShouldBe("tools");
    }
}
