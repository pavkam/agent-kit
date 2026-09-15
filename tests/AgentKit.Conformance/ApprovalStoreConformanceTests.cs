// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines mandatory atomicity and exact-binding behavior for approval stores.</summary>
/// <typeparam name="TFixture">The isolated implementation fixture.</typeparam>
public abstract class ApprovalStoreConformanceTests<TFixture>
    where TFixture : IApprovalStoreConformanceFixture
{
    /// <summary>Creates an isolated fixture for one contract case.</summary>
    /// <returns>The fresh fixture.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies create and resolution replay are idempotent while changed evidence conflicts.</summary>
    [Fact]
    public async Task ResolveAsync_WhenEvidenceIsReplayedOrChanged_PreservesFirstTerminalResponse()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateRequest();
        var response = CreateResponse(request);

        (await store.CreateAsync(request, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreCreateResult.Created);
        (await store.CreateAsync(request, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreCreateResult.AlreadyExists);
        (await store.ResolveAsync(response, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreResolveResult.Resolved);
        (await store.ResolveAsync(response, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreResolveResult.AlreadyResolved);
        var conflictingResponse = new ApprovalResponse(response.Id, response.RequestId, response.Binding,
            ApprovalResolution.Denied, response.ApproverIdentity, response.RespondedAt);
        (await store.ResolveAsync(conflictingResponse, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreResolveResult.Conflict);

        var retained = await store.ReadAsync(request.Id, TestContext.Current.CancellationToken);
        retained.Request.ShouldBe(request);
        retained.Response.ShouldBe(response);
    }

    /// <summary>Verifies changed request and response bindings cannot reuse stable identities.</summary>
    [Fact]
    public async Task CreateAndResolveAsync_WhenBindingChanges_RejectsChangedInput()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateRequest();
        var changedSecurityRequest = request.Binding.Request with { InputFingerprint = new InputFingerprint("sha256:changed") };
        var changedBinding = new ApprovalScopeBinding(changedSecurityRequest, request.Binding.PolicyVersion,
            request.Binding.RevocationVersion, request.Binding.NotBefore, request.Binding.ExpiresAt,
            request.Binding.AllowedUses);

        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);
        var changedRequest = new ApprovalRequest(request.Id, changedBinding, request.SafePresentation, request.CreatedAt);
        var originalResponse = CreateResponse(request);
        var changedResponse = new ApprovalResponse(originalResponse.Id, request.Id, changedBinding,
            originalResponse.Resolution, originalResponse.ApproverIdentity, originalResponse.RespondedAt);
        (await store.CreateAsync(changedRequest, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreCreateResult.Conflict);
        (await store.ResolveAsync(changedResponse, TestContext.Current.CancellationToken)).ShouldBe(ApprovalStoreResolveResult.Conflict);
    }

    /// <summary>Verifies independently materialized equivalent resource arrays preserve exact binding identity.</summary>
    [Fact]
    public async Task CreateAsync_WhenEquivalentBindingIsReconstructed_TreatsItAsTheSameRequest()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateRequest();
        var source = request.Binding.Request;
        var reconstructedSecurityRequest = new SecurityRequest(
            source.Id,
            source.Scope,
            source.ToolCallId,
            source.Identity,
            source.Audience,
            source.Kind,
            source.Effect,
            [.. source.Resources.Select(static resource => new ProtectedResource(resource.Kind, resource.Identifier))],
            source.InputFingerprint,
            source.Deadline,
            source.RequestedUses);
        var reconstructedBinding = new ApprovalScopeBinding(
            reconstructedSecurityRequest,
            request.Binding.PolicyVersion,
            request.Binding.RevocationVersion,
            request.Binding.NotBefore,
            request.Binding.ExpiresAt,
            request.Binding.AllowedUses);
        var reconstructedRequest = new ApprovalRequest(
            request.Id,
            reconstructedBinding,
            request.SafePresentation,
            request.CreatedAt);

        (await store.CreateAsync(request, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreCreateResult.Created);
        (await store.CreateAsync(reconstructedRequest, TestContext.Current.CancellationToken))
            .ShouldBe(ApprovalStoreCreateResult.AlreadyExists);
    }

    private static ApprovalRequest CreateRequest()
    {
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("requester"), ExecutionSubjectKind.Human);
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
        return new ApprovalRequest(new ApprovalRequestId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            binding, "Write workspace file", now);
    }

    private static ApprovalResponse CreateResponse(ApprovalRequest request) => new(
        new ApprovalResponseId(Guid.Parse("50000000-0000-0000-0000-000000000005")), request.Id,
        request.Binding, ApprovalResolution.Approved,
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("approver"), ExecutionSubjectKind.Human),
        request.CreatedAt.AddSeconds(1));
}
