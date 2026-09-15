// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

/// <summary>Verifies exact binding, responder authority, replay, expiry, and audit behavior of the approval broker.</summary>
public sealed class DefaultApprovalBrokerTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RequestAsync_WhenAuthorizedResponseMatches_CommitsAuditsAndReturnsApproval()
    {
        var request = CreateApprovalRequest();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var audit = new RecordingAuditDispatcher();
        var broker = CreateBroker(new InMemoryApprovalStore(), handler, new AllowResponderAuthorizer(), audit);

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerApproved>().Response.RequestId.ShouldBe(request.Id);
        audit.Records.Count.ShouldBe(1);
        audit.Records[0].EventKind.ShouldBe(SecurityAuditEventKind.Approval);
    }

    [Fact]
    public async Task RequestAsync_WhenTerminalResponseIsRetained_ReplaysWithoutDispatchingAgain()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Denied));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());
        _ = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        var replay = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        _ = replay.ShouldBeOfType<ApprovalBrokerDenied>();
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task RequestAsync_WhenResponderIsUnauthorized_DoesNotCommitCandidateResponse()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var broker = CreateBroker(
            store,
            new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved)),
            new DenyResponderAuthorizer(),
            new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ApprovalBrokerUnavailable>();
        (await store.ReadAsync(request.Id, TestContext.Current.CancellationToken)).Response.ShouldBeNull();
    }

    [Fact]
    public async Task RequestAsync_WhenRequestExpired_DoesNotPersistOrDispatch()
    {
        var request = CreateApprovalRequest(expiresAt: _now);
        var store = new InMemoryApprovalStore();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ApprovalBrokerExpired>();
        (await store.ReadAsync(request.Id, TestContext.Current.CancellationToken)).Request.ShouldBeNull();
        handler.CallCount.ShouldBe(0);
    }

    private static DefaultApprovalBroker CreateBroker(
        IApprovalStore store,
        IApprovalHandler handler,
        IApprovalResponderAuthorizer authorizer,
        ISecurityAuditDispatcher auditDispatcher) => new(
        store,
        handler,
        authorizer,
        auditDispatcher,
        new FixedAuditRecordIdGenerator(),
        new FakeTimeProvider(_now));

    private static ApprovalRequest CreateApprovalRequest(DateTimeOffset? expiresAt = null)
    {
        var securityRequest = SecurityAuthorityTestData.CreateRequest(_now);
        var binding = new ApprovalScopeBinding(
            securityRequest,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            expiresAt is { } expiry && expiry <= _now ? _now.AddMinutes(-1) : _now,
            expiresAt ?? _now.AddMinutes(5),
            1);
        return new ApprovalRequest(
            new ApprovalRequestId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
            binding,
            "Approve a bounded test operation.",
            _now);
    }

    private static ApprovalResponse CreateResponse(ApprovalRequest request, ApprovalResolution resolution) => new(
        new ApprovalResponseId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
        request.Id,
        request.Binding,
        resolution,
        request.Binding.Request.Identity,
        _now.AddSeconds(1));

    private sealed class RespondingHandler(Func<ApprovalRequest, ApprovalResponse> responseFactory): IApprovalHandler
    {
        public int CallCount { get; private set; }

        public ValueTask<ApprovalHandlerResult> TryResolveAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult<ApprovalHandlerResult>(new ApprovalHandlerResponded(responseFactory(request)));
        }
    }

    private sealed class AllowResponderAuthorizer: IApprovalResponderAuthorizer
    {
        public ValueTask<ApprovalResponderAuthorizationResult> AuthorizeAsync(
            ApprovalRequest request,
            ApprovalResponse candidateResponse,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ApprovalResponderAuthorizationResult>(new ApprovalResponderAuthorized());
    }

    private sealed class DenyResponderAuthorizer: IApprovalResponderAuthorizer
    {
        public ValueTask<ApprovalResponderAuthorizationResult> AuthorizeAsync(
            ApprovalRequest request,
            ApprovalResponse candidateResponse,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ApprovalResponderAuthorizationResult>(
                new ApprovalResponderUnauthorized("The responder lacks approval authority."));
    }

    private sealed class RecordingAuditDispatcher: ISecurityAuditDispatcher
    {
        public List<SecurityAuditRecord> Records { get; } = [];

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Records.Add(record);
            return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
        }
    }

    private sealed class FixedAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() =>
            new(Guid.Parse("61000000-0000-0000-0000-000000000006"));
    }
}
