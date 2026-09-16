// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Runs approval-store conformance against explicitly ephemeral process-local storage.</summary>
public sealed class InMemoryApprovalStoreTests: ApprovalStoreConformanceTests<InMemoryApprovalStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override InMemoryApprovalStoreConformanceFixture CreateFixture() => new();

    /// <summary>Verifies the adapter never claims process-loss durability.</summary>
    [Fact]
    public async Task Capabilities_WhenRead_ReportsEphemeralStorage()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        store.Capabilities.IsDurable.ShouldBeFalse();
    }

    /// <summary>Verifies resolving a response for a request that was never created fails closed as not found.</summary>
    [Fact]
    public async Task ResolveAsync_WhenNoMatchingRequestWasCreated_ReturnsNotFound()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var identity = TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("requester"), ExecutionSubjectKind.Human);
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), null,
            new BeforeRunOperationCorrelation(new OperationId(Guid.Parse("20000000-0000-0000-0000-000000000002")), null));
        var securityRequest = new SecurityRequest(
            new SecurityRequestId(Guid.Parse("30000000-0000-0000-0000-000000000003")), scope, null,
            identity, new ComponentId("test"), SecurityOperationKind.FileWrite, SecurityEffect.CreateOrReplace,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:input"), now.AddMinutes(5));
        var binding = new ApprovalScopeBinding(securityRequest, new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), now, now.AddMinutes(5), 1);
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ApprovalRequestId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            binding,
            ApprovalResolution.Approved,
            identity,
            now.AddSeconds(1));

        var result = await store.ResolveAsync(response, TestContext.Current.CancellationToken);

        result.ShouldBe(ApprovalStoreResolveResult.NotFound);
    }
}
