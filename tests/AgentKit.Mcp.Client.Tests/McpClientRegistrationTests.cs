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

    [Fact]
    public void AddMcpCapabilityProfile_WhenRegistered_PublishesOneCapabilityProfileSourceReportingIt()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("docs"), options => options.Command = "server");
        _ = services.AddMcpCapabilityProfile(new CapabilityProfileId("local"), options => options.EndpointKeys.Add(new McpEndpointKey("docs")));
        _ = services.AddMcpCapabilityProfile(new CapabilityProfileId("other"), options => options.EndpointKeys.Add(new McpEndpointKey("docs")));

        using var provider = services.BuildServiceProvider();
        var source = provider.GetServices<IAgentCapabilityProfileSource>().ShouldHaveSingleItem();

        source.CapabilityId.ShouldBe(McpCapabilityIds.Client);
        source.Contains(new CapabilityProfileId("local")).ShouldBeTrue();
        source.Contains(new CapabilityProfileId("other")).ShouldBeTrue();
        source.Contains(new CapabilityProfileId("missing")).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 4_194_304L, 1)]
    [InlineData(1, 4_194_303L, 1)]
    [InlineData(1, 4_194_304L, 99)]
    public void AddMcpClient_WhenHttpBoundIsInvalid_ThrowsArgumentOutOfRangeException(
        int streamTimeoutSeconds,
        long maximumHttpResponseBytes,
        int classification)
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => services.AddMcpClient(options =>
        {
            options.HttpStreamTimeout = TimeSpan.FromSeconds(streamTimeoutSeconds);
            options.MaximumHttpResponseBytes = maximumHttpResponseBytes;
            options.HttpDataClassification = (NetworkDataClassification) classification;
        }));
    }

    [Fact]
    public void AddMcpHttpEndpoint_WhenRegistered_PublishesOnlyTheNetworkBackedTransportFactoryOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();

        _ = services.AddMcpHttpEndpoint(new McpEndpointKey("remote"), options => options.Endpoint = new Uri("https://mcp.example/rpc"));
        _ = services.AddMcpHttpEndpoint(new McpEndpointKey("other"), options => options.Endpoint = new Uri("https://mcp.example/other"));

        services.Where(static descriptor => descriptor.ServiceType == typeof(IMcpTransportFactory))
            .Select(static descriptor => descriptor.ImplementationType)
            .ShouldBe([typeof(HttpMcpTransportFactory)]);
    }

    [Fact]
    public void AddMcpStdioEndpoint_WhenRegisteredAlongsideHttp_PublishesBothFactoriesWithoutOneShadowingTheOther()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();

        _ = services.AddMcpHttpEndpoint(new McpEndpointKey("remote"), options => options.Endpoint = new Uri("https://mcp.example/rpc"));
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("local"), options => options.Command = "server");

        services.Where(static descriptor => descriptor.ServiceType == typeof(IMcpTransportFactory))
            .Select(static descriptor => descriptor.ImplementationType)
            .ShouldBe([typeof(HttpMcpTransportFactory), typeof(StdioMcpTransportFactory)]);
    }

    [Fact]
    public void AddMcpClient_WhenOnlyStdioEndpointIsRegistered_DoesNotRequireNetworkCollaborators()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpClient();
        _ = services.AddMcpStdioEndpoint(new McpEndpointKey("local"), options => options.Command = "server");

        services.Any(static descriptor => descriptor.ImplementationType == typeof(HttpMcpTransportFactory)).ShouldBeFalse();
    }

    [Fact]
    public void AddMcpHttpEndpoint_WhenNetworkTransportIsMissing_FailsClosedWhenFactoriesAreResolved()
    {
        var services = ComposeHttpClient();
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(() => provider.GetServices<IMcpTransportFactory>().ToArray());

        exception.Message.ShouldContain("INetwork");
    }

    [Fact]
    public void AddMcpHttpEndpoint_WhenNetworkCollaboratorsAreRegistered_ResolvesTheFactoryOverThem()
    {
        var grants = new ConsumingGrantStore();
        var services = ComposeHttpClient()
            .AddSingleton<INetworkNameResolver>(new FixedAddressNameResolver(grants, TimeProvider.System))
            .AddSingleton<INetworkTransport>(new HandlerNetworkTransport(new StubHttpMessageHandler(_ => new HttpResponseMessage()), grants));
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<IMcpTransportFactory>().ShouldHaveSingleItem().ShouldBeOfType<HttpMcpTransportFactory>();
    }

    private static ServiceCollection ComposeHttpClient()
    {
        var grants = new ConsumingGrantStore();
        var services = new ServiceCollection();
        _ = services.AddLogging()
            .AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(new GrantingSecurityAuthority(grants)))
            .AddSingleton<ISecurityGrantStore>(grants)
            .AddSingleton<ISecurityAuditDispatcher>(new RecordingAuditDispatcher())
            .AddSingleton<IIdentifierGenerator<SecurityRequestId>>(new SecurityRequestIdSource())
            .AddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(new AuditRecordIdSource())
            .AddMcpClient()
            .AddMcpHttpEndpoint(new McpEndpointKey("remote"), options => options.Endpoint = new Uri("https://mcp.example/rpc"));
        return services;
    }

    private sealed class SecurityRequestIdSource: IIdentifierGenerator<SecurityRequestId>
    {
        public SecurityRequestId Create() => new(Guid.NewGuid());
    }

    private sealed class AuditRecordIdSource: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() => new(Guid.NewGuid());
    }
}
