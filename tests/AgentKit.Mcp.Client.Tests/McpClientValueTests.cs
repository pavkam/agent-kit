// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

public sealed class McpClientValueTests
{
    [Fact]
    public void CatalogSnapshot_WhenValuesAreValid_CapturesImmutableState()
    {
        var tools = ImmutableArray.Create(
            new McpRemoteToolDescriptor(new McpToolName("weather.get"), new ToolVersion("2.1")));

        var snapshot = new McpClientToolCatalogSnapshot(
            new McpCatalogVersion(4),
            McpProtocolVersions.July2026,
            tools);

        snapshot.Version.ShouldBe(new McpCatalogVersion(4));
        snapshot.ProtocolVersion.ShouldBe(McpProtocolVersions.July2026);
        snapshot.Tools.ShouldBe(tools);
    }

    [Fact]
    public void CatalogSnapshot_WhenToolListIsEmpty_AllowsPublishedEmptyView()
    {
        var snapshot = new McpClientToolCatalogSnapshot(
            new McpCatalogVersion(1),
            McpProtocolVersions.November2025,
            []);

        snapshot.Tools.ShouldBeEmpty();
    }

    [Fact]
    public void CatalogSnapshot_WhenCatalogVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new McpClientToolCatalogSnapshot(default, McpProtocolVersions.July2026, []));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void CatalogSnapshot_WhenProtocolVersionIsUninitialized_ThrowsForProtocolVersion()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new McpClientToolCatalogSnapshot(new McpCatalogVersion(1), default, []));

        exception.ParamName.ShouldBe("protocolVersion");
    }

    [Fact]
    public void CatalogSnapshot_WhenToolsAreDefault_ThrowsForTools()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new McpClientToolCatalogSnapshot(
                new McpCatalogVersion(1),
                McpProtocolVersions.July2026,
                default));

        exception.ParamName.ShouldBe("tools");
    }

    [Fact]
    public void RemoteToolDescriptor_WhenValuesAreValid_CapturesIdentity()
    {
        var descriptor = new McpRemoteToolDescriptor(
            new McpToolName("weather.get"),
            new ToolVersion("2.1"));

        descriptor.Name.ShouldBe(new McpToolName("weather.get"));
        descriptor.Version.ShouldBe(new ToolVersion("2.1"));
    }

    [Fact]
    public void RemoteToolDescriptor_WhenNameIsUninitialized_ThrowsForName()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new McpRemoteToolDescriptor(default, new ToolVersion("1")));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void RemoteToolDescriptor_WhenVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new McpRemoteToolDescriptor(new McpToolName("tool"), default));

        exception.ParamName.ShouldBe("version");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ContractMismatchException_WhenMessageIsMissing_ThrowsForMessage(string? message)
    {
        var exception = Should.Throw<ArgumentException>(() => new McpToolContractMismatchException(message!));

        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void ContractMismatchException_WhenMessageIsValid_PreservesMessage()
    {
        var exception = new McpToolContractMismatchException("catalog mismatch");

        exception.Message.ShouldBe("catalog mismatch");
    }

    [Fact]
    public void ToolInvocationException_WhenToolNameIsUninitialized_ThrowsForToolName()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new McpToolInvocationException(default, "failed"));

        exception.ParamName.ShouldBe("toolName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ToolInvocationException_WhenMessageIsMissing_ThrowsForMessage(string? message)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new McpToolInvocationException(new McpToolName("tool"), message!));

        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void ToolInvocationException_WhenValuesAreValid_PreservesToolAndMessage()
    {
        var exception = new McpToolInvocationException(new McpToolName("tool"), "failed");

        exception.ToolName.ShouldBe(new McpToolName("tool"));
        exception.Message.ShouldBe("failed");
    }

    [Fact]
    public void ToolClientFactory_WhenContractIsNull_ThrowsForContract()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new McpToolClientFactory<SimpleTools>(null!));

        exception.ParamName.ShouldBe("contract");
    }

    [Fact]
    public void AddMcpToolClient_WhenServicesAreNull_ThrowsForServices()
    {
        IServiceCollection services = null!;

        IServiceCollection Act() => services.AddMcpToolClient<SimpleTools>();

        var exception = Should.Throw<ArgumentNullException>(Act);

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void InternalRemoteTool_WhenNameIsUninitialized_ThrowsForName()
    {
        var exception = Should.Throw<ArgumentException>(() => new McpRemoteTool(default, null));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void InternalRemoteTool_WhenVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new McpRemoteTool(new McpToolName("tool"), default(ToolVersion)));

        exception.ParamName.ShouldBe("version");
    }

    private abstract class SimpleTools
    {
        [McpTool("simple", "1")]
        public abstract Task<SimpleResponse> ExecuteAsync(SimpleRequest request);
    }

    private sealed record SimpleRequest(string Value);

    private sealed record SimpleResponse(string Value);
}
