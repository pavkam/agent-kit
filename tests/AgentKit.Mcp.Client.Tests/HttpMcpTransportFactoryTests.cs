// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

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
            provider,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILoggerFactory>());
        var endpoint = CreateHttpEndpoint();
        var openRequest = CreateSessionOpenRequest(agentId, provider, endpoint);
        var transportRequest = new McpTransportOpenRequest(openRequest, endpoint);
        var result = await factory.OpenAsync(transportRequest, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<McpTransportOpenDenied>();
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
