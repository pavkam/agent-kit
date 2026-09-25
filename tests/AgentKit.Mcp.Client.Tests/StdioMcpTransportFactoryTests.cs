// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

/// <summary>Verifies <see cref="StdioMcpTransportFactory"/> fail-closed behavior without a process executor.</summary>
public sealed class StdioMcpTransportFactoryTests
{
    [Fact]
    public async Task OpenAsync_WhenProcessExecutorIsMissing_ReturnsTransportFailure()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IIdentifierGenerator<SecurityRequestId>, TestSecurityRequestIdGenerator>()
            .AddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, TestSecurityAuditRecordIdGenerator>()
            .AddSingleton<IIdentifierGenerator<ProcessOperationId>, TestProcessOperationIdGenerator>()
            .AddSingleton<ISecurityAuditDispatcher, NoOpSecurityAuditDispatcher>()
            .AddInMemorySecurityGrantStore()
            .AddInMemoryApprovalStore()
            .AddInMemorySecurityDecisionStore()
            .AddAgentPermissions()
            .AddStandaloneSecurityProfile(
                new AgentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                new AgentDefinitionRevision(1),
                new ConfigurationVersion(1),
                new SecurityProfileKey("stdio-test"),
                new ComponentKey<ISecurityAuthority>("test-authority"))
            .AddAllowAllSecurityPolicy()
            .AddSingleton<IProcessExecutorSelector, MissingProcessExecutorSelector>()
            .AddMcpClient();
        await using var provider = services.BuildServiceProvider();
        var factory = new StdioMcpTransportFactory(
            provider.GetRequiredService<IProcessExecutorSelector>(),
            provider.GetRequiredService<ISecurityAuthoritySelector>(),
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityRequestId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<IIdentifierGenerator<ProcessOperationId>>(),
            provider.GetRequiredService<McpClientOptionsSnapshot>(),
            provider.GetRequiredService<TimeProvider>());
        var endpoint = CreateStdioEndpoint();
        var openRequest = CreateSessionOpenRequest(endpoint);
        var result = await factory.OpenAsync(
            new McpTransportOpenRequest(openRequest, endpoint),
            TestContext.Current.CancellationToken);
        (result is McpTransportOpenFailed or McpTransportOpenDenied).ShouldBeTrue();
    }

    private static McpEndpoint CreateStdioEndpoint() =>
        new(
            new McpEndpointKey("local"),
            new McpEndpointRevision(1),
            new McpStdioTransportProfile("npx", ["-y", "example-mcp"]),
            authentication: null,
            new McpEndpointBounds(
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(10),
                maximumFrameBytes: 1_048_576,
                maximumMessageBytes: 4_194_304,
                maximumInFlightRequests: 16));

    private static McpClientOpenRequest CreateSessionOpenRequest(McpEndpoint endpoint)
    {
        var agentId = new AgentId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(
            new OperationId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            admissionId: null);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("stdio-test"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:stdio-mcp-transport-test")),
            new ComponentKey<ISecurityAuthority>("test-authority"),
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

    private sealed class TestSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
    {
        public SecurityRequestId Create() => new(Guid.NewGuid());
    }

    private sealed class TestSecurityAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() => new(Guid.NewGuid());
    }

    private sealed class TestProcessOperationIdGenerator: IIdentifierGenerator<ProcessOperationId>
    {
        public ProcessOperationId Create() => new(Guid.NewGuid());
    }

    private sealed class MissingProcessExecutorSelector: IProcessExecutorSelector
    {
        public ValueTask<ProcessExecutorSelectionResult> SelectAsync(
            ProcessExecutorKey key,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProcessExecutorSelectionResult>(new ProcessExecutorMissing(key));
    }

}
