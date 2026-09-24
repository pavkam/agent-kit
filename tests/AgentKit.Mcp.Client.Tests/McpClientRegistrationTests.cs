// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="McpClientRegistration"/> behavior through public extensions.</summary>
public sealed class McpClientRegistrationTests
{
    [Fact]
    public void AddMcpClient_WhenFrameLimitExceedsMessageLimit_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => services.AddMcpClient(options =>
        {
            options.MaximumFrameBytes = 2;
            options.MaximumMessageBytes = 1;
        }));

        exception.ParamName.ShouldBe("maximumFrameBytes");
    }

    [Fact]
    public void AddMcpStdioEndpoint_WhenCommandIsMissing_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        var exception = Should.Throw<ArgumentException>(() =>
            services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), _ => { }));

        exception.ParamName.ShouldBe("Command");
    }

    [Fact]
    public void AddMcpStdioEndpoint_WhenRegisteredTwiceWithSameConfiguration_IsIdempotent()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options =>
        {
            options.Command = "server";
            options.Arguments.Add("--stdio");
        });
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options =>
        {
            options.Command = "server";
            options.Arguments.Add("--stdio");
        });

        services.Count(static descriptor =>
            descriptor.IsKeyedService && descriptor.ServiceType == typeof(McpEndpoint)).ShouldBe(1);
    }

    [Fact]
    public void AddMcpStdioEndpoint_WhenKeyConflictsWithoutReplace_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options => options.Command = "one");
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options => options.Command = "two"));

        exception.Message.ShouldContain("already registered");
    }

    [Fact]
    public void ReplaceMcpStdioEndpoint_WhenCalled_IncrementsRevision()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options => options.Command = "one");
        _ = services.ReplaceMcpStdioEndpoint(new McpEndpointKey("docs"), options => options.Command = "two");
        using var provider = services.BuildServiceProvider();
        var endpoint = provider.GetRequiredKeyedService<McpEndpoint>("docs");
        endpoint.Revision.Value.ShouldBe(2);
        endpoint.Transport.ShouldBeOfType<McpStdioTransportProfile>().Command.ShouldBe("two");
    }

    [Fact]
    public void AddMcpHttpEndpoint_WhenEndpointIsMissing_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddMcpHttpEndpoint(new McpEndpointKey("remote"), _ => { }));

        exception.Message.ShouldContain("Endpoint");
    }

    [Fact]
    public void AddMcpCapabilityProfile_WhenDuplicateEndpointKeys_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var key = new McpEndpointKey("docs");
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddMcpCapabilityProfile(new CapabilityProfileId("local"), options =>
            {
                options.EndpointKeys.Add(key);
                options.EndpointKeys.Add(key);
            }));

        exception.Message.ShouldContain("unique");
    }
}
