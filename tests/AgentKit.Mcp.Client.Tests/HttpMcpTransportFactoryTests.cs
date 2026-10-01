// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using System.Net.Http;

using ModelContextProtocol.Client;

/// <summary>Verifies <see cref="HttpMcpTransportFactory"/> connect authorization.</summary>
public sealed class HttpMcpTransportFactoryTests
{
    [Fact]
    public async Task OpenAsync_WhenConnectIsDenied_ReturnsDeniedBeforeTransportCreation()
    {
        var agentId = new AgentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var authorityKey = new ComponentKey<ISecurityAuthority>("deny-authority");
        var profileKey = new SecurityProfileKey("deny-profile");
        var snapshot = new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.NewGuid()),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:deny-http-mcp-transport-test"));
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>()
            .AddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>()
            .AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>()
            .AddSingleton<ISecurityAuditDispatcher, NoOpSecurityAuditDispatcher>()
            .AddInMemorySecurityGrantStore()
            .AddInMemoryApprovalStore()
            .AddInMemorySecurityDecisionStore()
            .AddAgentPermissions(options => options.PolicySnapshot = snapshot)
            .AddSingleton<ISecurityAuthority, DenyAllSecurityAuthority>()
            .AddSecurityAuthority(authorityKey)
            .AddSecurityProfilePublication(new SecurityProfilePublication(
                agentId,
                new AgentDefinitionRevision(1),
                new ConfigurationVersion(1),
                profileKey,
                new SecurityProfileVersion(1),
                snapshot,
                authorityKey));
        await using var provider = services.BuildServiceProvider();
        var factory = new HttpMcpTransportFactory(
            provider.GetRequiredService<ISecurityAuthoritySelector>(),
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityRequestId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            new FixedNetworkOperationIds(),
            new FixedAddressNameResolver(new ConsumingGrantStore(), TimeProvider.System),
            new HandlerNetworkTransport(new StubHttpMessageHandler(_ => throw new InvalidOperationException("No I/O expected.")), new ConsumingGrantStore()),
            NetworkMcpHttpHandlerHarness.CreateOptions(),
            provider,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILoggerFactory>());
        var endpoint = CreateHttpEndpoint();
        var openRequest = CreateSessionOpenRequest(agentId, provider, endpoint);
        var transportRequest = new McpTransportOpenRequest(openRequest, endpoint);
        var result = await factory.OpenAsync(transportRequest, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<McpTransportOpenDenied>();
    }

    [Fact]
    public async Task OpenAsync_WhenAuthorized_RoutesEverySdkExchangeThroughTheNetworkTransport()
    {
        var server = new StubMcpHttpServer();
        var fixture = Fixture.Create(server);

        var opened = (await fixture.Factory.OpenAsync(fixture.Request(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<McpTransportOpened>();
        var sdkTransport = opened.Transport.ShouldBeOfType<SdkMcpClientTransport>();
        await using (var client = await McpClient.CreateAsync(sdkTransport.ClientTransport, cancellationToken: TestContext.Current.CancellationToken))
        {
            var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
            tools.ShouldBeEmpty();
        }

        await opened.Transport.DisposeAsync();

        server.Exchanges.ShouldContain("initialize");
        server.Exchanges.ShouldContain("tools/list");
        fixture.Transport.Requests.Count.ShouldBe(server.Exchanges.Count);
        fixture.Transport.Requests.ShouldAllBe(static request => request.Destination.Host.Value == "mcp.example"
            && request.Classification == NetworkDataClassification.Confidential
            && request.Bounds.Response.MaximumRedirects == 0);
        fixture.Resolver.Resolved.Count.ShouldBe(server.Exchanges.Count);
        // One connect grant plus a resolution and a send grant per exchange, each consumed by its own boundary.
        fixture.Authority.Requests.Count.ShouldBe(1 + (2 * server.Exchanges.Count));
        fixture.Authority.Requests[0].Audience.ShouldBe(new ComponentId("agentkit.mcp.client.session"));
    }

    [Fact]
    public async Task OpenAsync_WhenOAuthEndpoint_SendsTheTokenOnlyInsideTheNetworkRequestHeaders()
    {
        const string Token = "oauth-token-needle";
        var server = new StubMcpHttpServer();
        var fixture = Fixture.Create(server, token: Token);

        var opened = (await fixture.Factory.OpenAsync(fixture.Request(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<McpTransportOpened>();
        var sdkTransport = opened.Transport.ShouldBeOfType<SdkMcpClientTransport>();
        await using (var client = await McpClient.CreateAsync(sdkTransport.ClientTransport, cancellationToken: TestContext.Current.CancellationToken))
        {
            _ = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        await opened.Transport.DisposeAsync();

        fixture.Transport.Requests.ShouldAllBe(static request => request.Headers.Headers.Any(
            static header => header.Name == "Authorization" && header.Value == "Bearer oauth-token-needle"));
        foreach (var security in fixture.Authority.Requests)
        {
            security.Resources.ShouldAllBe(static resource => !resource.Identifier.Contains("needle", StringComparison.Ordinal));
        }

        fixture.Audit.Records.ShouldAllBe(static record => !record.Fields.Values.Any(
            static value => value.ToString()!.Contains("needle", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task OpenAsync_WhenNetworkGrantIsDenied_SurfacesATransportFailureWithoutSending()
    {
        var server = new StubMcpHttpServer();
        var fixture = Fixture.Create(server);
        fixture.Authority.Deny = static request => request.Audience.Value == "test.network.transport";

        var opened = (await fixture.Factory.OpenAsync(fixture.Request(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<McpTransportOpened>();
        var sdkTransport = opened.Transport.ShouldBeOfType<SdkMcpClientTransport>();

        _ = await Should.ThrowAsync<Exception>(async () =>
            await McpClient.CreateAsync(sdkTransport.ClientTransport, cancellationToken: TestContext.Current.CancellationToken));

        server.Exchanges.ShouldBeEmpty();
        fixture.Transport.Requests.ShouldBeEmpty();
        await opened.Transport.DisposeAsync();
    }

    [Fact]
    public async Task OpenAsync_WhenOAuthProviderIsMissing_ReturnsFailureBeforeAnyNetworkEffect()
    {
        var fixture = Fixture.Create(new StubMcpHttpServer(), registerTokenProvider: false);

        var result = await fixture.Factory.OpenAsync(fixture.Request(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<McpTransportOpenFailed>();
        fixture.Transport.Requests.ShouldBeEmpty();
    }

    private sealed class Fixture
    {
        private readonly McpEndpoint _endpoint;
        private readonly McpClientOpenRequest _open;

        private Fixture(
            HttpMcpTransportFactory factory,
            ConsumingGrantStore grants,
            GrantingSecurityAuthority authority,
            RecordingAuditDispatcher audit,
            FixedAddressNameResolver resolver,
            HandlerNetworkTransport transport,
            McpEndpoint endpoint)
        {
            Factory = factory;
            Authority = authority;
            Audit = audit;
            Resolver = resolver;
            Transport = transport;
            _ = grants;
            _endpoint = endpoint;
            var profile = new McpCapabilityProfile(
                McpCapabilityIds.Client,
                new CapabilityProfileId("local"),
                new McpCapabilityProfileRevision(1),
                [endpoint.Key]);
            _open = new McpClientOpenRequest(profile, endpoint, ProviderEgressHarness.Operation);
        }

        internal HttpMcpTransportFactory Factory { get; }

        internal GrantingSecurityAuthority Authority { get; }

        internal RecordingAuditDispatcher Audit { get; }

        internal FixedAddressNameResolver Resolver { get; }

        internal HandlerNetworkTransport Transport { get; }

        internal McpTransportOpenRequest Request() => new(_open, _endpoint);

        internal static Fixture Create(HttpMessageHandler server, string? token = null, bool registerTokenProvider = true)
        {
            var grants = new ConsumingGrantStore();
            var authority = new GrantingSecurityAuthority(grants);
            var audit = new RecordingAuditDispatcher();
            var resolver = new FixedAddressNameResolver(grants, TimeProvider.System);
            var transport = new HandlerNetworkTransport(server, grants);
            var options = NetworkMcpHttpHandlerHarness.CreateOptions();
            var endpoint = new McpEndpoint(
                new McpEndpointKey("remote"),
                new McpEndpointRevision(1),
                new McpHttpTransportProfile(NetworkMcpHttpHandlerHarness.Endpoint),
                token is null && registerTokenProvider ? null : new McpAuthenticationReference("oauth", "https://mcp.example"),
                options.CreateEndpointBounds());
            var services = new ServiceCollection();
            if (registerTokenProvider && token is not null)
            {
                _ = services.AddKeyedSingleton<IOAuthAccessTokenProvider>("oauth", new FixedTokenProvider(token));
            }

            var provider = services.BuildServiceProvider();
            var factory = new HttpMcpTransportFactory(
                new FixedSecurityAuthoritySelector(authority),
                grants,
                audit,
                new SequentialSecurityRequestIds(),
                new SequentialAuditRecordIds(),
                new SequentialIntentIds(),
                new SequentialNetworkOperationIds(),
                resolver,
                transport,
                options,
                provider,
                TimeProvider.System,
                NullLoggerFactory.Instance);
            return new Fixture(factory, grants, authority, audit, resolver, transport, endpoint);
        }
    }

    private sealed class FixedTokenProvider(string token): IOAuthAccessTokenProvider
    {
        public ValueTask<OAuthTokenProviderCredential> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new OAuthTokenProviderCredential(token, expiresAtUtc: null));
    }

    private sealed class SequentialSecurityRequestIds: IIdentifierGenerator<SecurityRequestId>
    {
        private int _value;

        public SecurityRequestId Create() => new(Guid.Parse($"23000000-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }

    private sealed class SequentialAuditRecordIds: IIdentifierGenerator<SecurityAuditRecordId>
    {
        private int _value;

        public SecurityAuditRecordId Create() => new(Guid.Parse($"53000000-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }

    private sealed class SequentialIntentIds: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        private int _value;

        public SecurityEnforcementIntentId Create() => new(Guid.Parse($"43000000-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }

    private sealed class SequentialNetworkOperationIds: IIdentifierGenerator<NetworkOperationId>
    {
        private int _value;

        public NetworkOperationId Create() => new(Guid.Parse($"33000000-0000-0000-0000-{Interlocked.Increment(ref _value):D12}"));
    }

    private static McpEndpoint CreateHttpEndpoint() =>
        new(
            new McpEndpointKey("remote"),
            new McpEndpointRevision(1),
            new McpHttpTransportProfile(new Uri("https://mcp.example/rpc")),
            authentication: null,
            new McpEndpointBounds(
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(10),
                maximumFrameBytes: 1_048_576,
                maximumMessageBytes: 4_194_304,
                maximumInFlightRequests: 16));

    private static McpClientOpenRequest CreateSessionOpenRequest(
        AgentId agentId,
        ServiceProvider provider,
        McpEndpoint endpoint)
    {
        var permissionOptions = provider.GetRequiredService<IOptions<AgentPermissionOptions>>().Value;
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            admissionId: null);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("deny-profile"),
            new SecurityProfileVersion(1),
            permissionOptions.PolicySnapshot!,
            new ComponentKey<ISecurityAuthority>("deny-authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId: null, correlation),
            identity);
        var operation = new ProtectedSemanticOperationContext(
            agentId,
            sessionId: null,
            conversationId: null,
            identity,
            correlation,
            authorization);
        var profile = new McpCapabilityProfile(
            McpCapabilityIds.Client,
            new CapabilityProfileId("local"),
            new McpCapabilityProfileRevision(1),
            [endpoint.Key]);
        return new McpClientOpenRequest(profile, endpoint, operation);
    }

    private sealed class FixedNetworkOperationIds: IIdentifierGenerator<NetworkOperationId>
    {
        public NetworkOperationId Create() => new(Guid.NewGuid());
    }

    private sealed class NoOpSecurityAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private sealed class GuidSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
    {
        public SecurityRequestId Create() => new(Guid.NewGuid());
    }

    private sealed class GuidSecurityAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() => new(Guid.NewGuid());
    }
}
