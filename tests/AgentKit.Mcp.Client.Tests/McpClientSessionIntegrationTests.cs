// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>Verifies <see cref="McpClientSession"/> against a loopback SDK server.</summary>
public sealed class McpClientSessionIntegrationTests
{
    [Fact]
    public async Task OpenAsync_WhenServerIsAvailable_InitializesAndListsTools()
    {
        var pair = PipeMcpTransportPair.CreateConnected();
        var pipeFactory = new PipeMcpTransportFactory(_ => ValueTask.FromResult(pair));
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var endpointKey = new McpEndpointKey("test");
        var profileId = new CapabilityProfileId("local");
        var services = new ServiceCollection()
            .AddLogging()
            .AddMcpClient()
            .AddStandaloneSecurityProfile(
                agentId,
                new AgentDefinitionRevision(1),
                new ConfigurationVersion(1),
                new SecurityProfileKey("test-security"),
                new ComponentKey<ISecurityAuthority>("test-authority"))
            .AddAllowAllSecurityPolicy()
            .AddInMemorySecurityGrantStore()
            .AddInMemoryApprovalStore()
            .AddInMemorySecurityDecisionStore()
            .AddSingleton<ISecurityAuditDispatcher, NoOpSecurityAuditDispatcher>()
            .AddMcpStdioEndpoint(endpointKey, static o => o.Command = "test")
            .AddMcpCapabilityProfile(profileId, o => o.EndpointKeys.Add(new McpEndpointKey("test")));
        foreach (var descriptor in services.Where(static d => d.ServiceType == typeof(IMcpTransportFactory)).ToArray())
        {
            _ = services.Remove(descriptor);
        }

        _ = services.AddSingleton<IMcpTransportFactory>(pipeFactory);
        await using var provider = services.BuildServiceProvider();
        _ = StartEchoServer(pair, provider.GetRequiredService<ILoggerFactory>(), TestContext.Current.CancellationToken);
        var endpointCatalog = provider.GetRequiredService<IMcpEndpointCatalog>();
        var profileCatalog = provider.GetRequiredService<IMcpCapabilityProfileCatalog>();
        var endpoint = (await endpointCatalog.ResolveAsync(endpointKey, TestContext.Current.CancellationToken))
            .ShouldBeOfType<McpEndpointResolved>().Endpoint;
        var profile = (await profileCatalog.ResolveAsync(
                new AgentCapabilityReference(McpCapabilityIds.Client, profileId),
                TestContext.Current.CancellationToken))
            .ShouldBeOfType<McpCapabilityProfileResolved>().Profile;
        var factory = provider.GetRequiredService<IMcpClientSessionFactory>();
        var permissionOptions = provider.GetRequiredService<IOptions<AgentPermissionOptions>>().Value;
        var openRequest = new McpClientOpenRequest(
            profile,
            endpoint,
            CreateOperation(agentId, permissionOptions.PolicySnapshot!));
        await using var session = await factory.OpenAsync(openRequest, TestContext.Current.CancellationToken);
        _ = await session.InitializeAsync(TestContext.Current.CancellationToken);
        var catalog = await session.GetCatalogAsync(TestContext.Current.CancellationToken);
        catalog.Tools.ShouldNotBeEmpty();
    }

    private static Task StartEchoServer(
        PipeMcpTransportPair pair,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var tool = McpServerTool.Create(
            static (EchoRequest request) => new EchoResponse(request.Message.ToUpperInvariant()),
            new McpServerToolCreateOptions { Name = "echo.run", UseStructuredContent = true });
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "test-server", Version = "1.0" },
            ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal),
        };
        options.ToolCollection.Add(tool);
        var server = McpServer.Create(
            new StreamServerTransport(pair.ServerInput, pair.ServerOutput),
            options,
            loggerFactory);
        return server.RunAsync(cancellationToken);
    }

    private sealed record EchoRequest(string Message);
    private sealed record EchoResponse(string Message);

    private sealed class NoOpSecurityAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private static ProtectedSemanticOperationContext CreateOperation(
        AgentId agentId,
        SecurityPolicySnapshotReference policySnapshot)
    {
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            admissionId: null);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("test-security"),
            new SecurityProfileVersion(1),
            policySnapshot,
            new ComponentKey<ISecurityAuthority>("test-authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId: null, correlation),
            identity);
        return new ProtectedSemanticOperationContext(
            agentId,
            sessionId: null,
            conversationId: null,
            identity,
            correlation,
            authorization);
    }
}
