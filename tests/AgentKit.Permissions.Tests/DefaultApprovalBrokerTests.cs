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

    [Fact]
    public async Task RequestAsync_WhenHandlerOutlivesBindingExpiry_CancelsTheWaitAndReturnsExpired()
    {
        var request = CreateApprovalRequest(expiresAt: _now.AddSeconds(30));
        var store = new InMemoryApprovalStore();
        var handler = new HangingHandler();
        var timeProvider = new FakeTimeProvider(_now);
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher(), timeProvider);

        var pending = broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        pending.IsCompleted.ShouldBeFalse("the handler must still be waiting before the binding expires");

        timeProvider.Advance(TimeSpan.FromSeconds(31));
        var completed = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        completed.ShouldBeSameAs(pending, "the handler wait must be bounded by the approval binding's expiry, not block indefinitely");
        _ = (await pending).ShouldBeOfType<ApprovalBrokerExpired>();
    }

    [Fact]
    public async Task RequestAsync_WhenStoreLacksTrustedControlPlane_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new FakeApprovalStore(capabilities: new ApprovalStoreCapabilities(IsDurable: true, ProvidesTrustedControlPlane: false));
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason
            .ShouldBe("Approval storage does not provide trusted control-plane access.");
    }

    [Fact]
    public async Task RequestAsync_WhenCreateThrowsOperationCanceled_Propagates()
    {
        var request = CreateApprovalRequest();
        var store = new FakeApprovalStore(create: (_, _) => throw new OperationCanceledException());
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task RequestAsync_WhenCreateThrowsException_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new FakeApprovalStore(create: (_, _) => throw new InvalidOperationException("boom"));
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason.ShouldBe("Approval storage is unavailable.");
    }

    [Fact]
    public async Task RequestAsync_WhenCreateReturnsConflict_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new FakeApprovalStore(create: (_, _) => ValueTask.FromResult(ApprovalStoreCreateResult.Conflict));
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason
            .ShouldBe("The approval request identity is already bound to different evidence.");
    }

    [Fact]
    public async Task RequestAsync_WhenReadAfterCreateThrows_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new FakeApprovalStore(read: (_, _) => throw new InvalidOperationException("boom"));
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason.ShouldBe("Approval storage is unavailable.");
    }

    [Fact]
    public async Task RequestAsync_WhenReadAfterCreateThrowsOperationCanceled_Propagates()
    {
        var request = CreateApprovalRequest();
        var store = new FakeApprovalStore(read: (_, _) => throw new OperationCanceledException());
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task RequestAsync_WhenRetainedRequestDiffersFromRequest_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var differentRequest = new ApprovalRequest(request.Id, request.Binding, "A different presentation.", _now);
        var store = new FakeApprovalStore(
            read: (_, _) => ValueTask.FromResult(new ApprovalStoreReadResult(differentRequest, null)));
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason
            .ShouldBe("Retained approval evidence does not match the request.");
    }

    [Fact]
    public async Task RequestAsync_WhenExpiresBetweenCreateAndRetainedCheck_ReturnsExpired()
    {
        var request = CreateApprovalRequest();
        var timeProvider = new FakeTimeProvider(_now);
        FakeApprovalStore? store = null;
        store = new FakeApprovalStore(
            read: (id, token) =>
            {
                timeProvider.SetUtcNow(request.Binding.ExpiresAt);
                return store!.PassthroughReadAsync(id, token);
            });
        var broker = new DefaultApprovalBroker(
            store,
            new DenyApprovalHandler(),
            new AllowResponderAuthorizer(),
            new RecordingAuditDispatcher(),
            new FixedAuditRecordIdGenerator(),
            timeProvider);

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ApprovalBrokerExpired>();
    }

    [Fact]
    public async Task RequestAsync_WhenHandlerThrowsOperationCanceled_Propagates()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var handler = new ThrowingHandler(new OperationCanceledException());
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task RequestAsync_WhenHandlerThrowsException_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var handler = new ThrowingHandler(new InvalidOperationException("boom"));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason.ShouldBe("The approval channel failed.");
    }

    [Fact]
    public async Task RequestAsync_WhenHandlerCannotResolveInline_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var broker = CreateBroker(store, new DenyApprovalHandler(), new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason
            .ShouldBe("No approval channel resolved the request.");
    }

    [Fact]
    public async Task RequestAsync_WhenResponseRespondedAtReachesExpiry_ReturnsExpired()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var handler = new RespondingHandler(value => new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            value.Id,
            value.Binding,
            ApprovalResolution.Approved,
            value.Binding.Request.Identity,
            value.Binding.ExpiresAt));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ApprovalBrokerExpired>();
    }

    [Fact]
    public async Task RequestAsync_WhenResponseApproverTenantMismatchesRequest_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var mismatchedIdentity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("a-different-tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);
        var handler = new RespondingHandler(value => new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("51000000-0000-0000-0000-000000000005")),
            value.Id,
            value.Binding,
            ApprovalResolution.Approved,
            mismatchedIdentity,
            _now.AddSeconds(1)));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason
            .ShouldBe("The approval response does not match the retained request binding.");
    }

    [Fact]
    public async Task RequestAsync_WhenResponderAuthorizerThrowsOperationCanceled_Propagates()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var broker = CreateBroker(store, handler, new ThrowingResponderAuthorizer(new OperationCanceledException()), new RecordingAuditDispatcher());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task RequestAsync_WhenResponderAuthorizerThrowsException_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var store = new InMemoryApprovalStore();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var broker = CreateBroker(store, handler, new ThrowingResponderAuthorizer(new InvalidOperationException("boom")), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason.ShouldBe("Approval responder authorization failed.");
    }

    [Fact]
    public async Task RequestAsync_WhenResolveThrowsOperationCanceled_Propagates()
    {
        var request = CreateApprovalRequest();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var store = new FakeApprovalStore(resolve: (_, _) => throw new OperationCanceledException());
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task RequestAsync_WhenResolveThrowsException_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var store = new FakeApprovalStore(resolve: (_, _) => throw new InvalidOperationException("boom"));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason.ShouldBe("Approval storage is unavailable.");
    }

    [Fact]
    public async Task RequestAsync_WhenResolveReturnsNotFound_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var store = new FakeApprovalStore(resolve: (_, _) => ValueTask.FromResult(ApprovalStoreResolveResult.NotFound));
        var broker = CreateBroker(store, handler, new AllowResponderAuthorizer(), new RecordingAuditDispatcher());

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason
            .ShouldBe("The approval response could not be committed.");
    }

    [Fact]
    public async Task RequestAsync_WhenAuditDispatchThrowsOperationCanceled_Propagates()
    {
        var request = CreateApprovalRequest();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var broker = CreateBroker(
            new InMemoryApprovalStore(),
            handler,
            new AllowResponderAuthorizer(),
            new ThrowingAuditDispatcher(new OperationCanceledException()));

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => broker.RequestAsync(request, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task RequestAsync_WhenAuditDispatchThrowsException_ReturnsUnavailable()
    {
        var request = CreateApprovalRequest();
        var handler = new RespondingHandler(static value => CreateResponse(value, ApprovalResolution.Approved));
        var broker = CreateBroker(
            new InMemoryApprovalStore(),
            handler,
            new AllowResponderAuthorizer(),
            new ThrowingAuditDispatcher(new InvalidOperationException("boom")));

        var result = await broker.RequestAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ApprovalBrokerUnavailable>().SafeReason.ShouldBe("Required approval audit failed.");
    }

    private static DefaultApprovalBroker CreateBroker(
        IApprovalStore store,
        IApprovalHandler handler,
        IApprovalResponderAuthorizer authorizer,
        ISecurityAuditDispatcher auditDispatcher,
        TimeProvider? timeProvider = null) => new(
        store,
        handler,
        authorizer,
        auditDispatcher,
        new FixedAuditRecordIdGenerator(),
        timeProvider ?? new FakeTimeProvider(_now));

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

    /// <summary>An approval store whose operations are individually overridable, defaulting to an in-memory pass-through.</summary>
    private sealed class FakeApprovalStore(
        ApprovalStoreCapabilities? capabilities = null,
        Func<ApprovalRequest, CancellationToken, ValueTask<ApprovalStoreCreateResult>>? create = null,
        Func<ApprovalRequestId, CancellationToken, ValueTask<ApprovalStoreReadResult>>? read = null,
        Func<ApprovalResponse, CancellationToken, ValueTask<ApprovalStoreResolveResult>>? resolve = null): IApprovalStore
    {
        private readonly InMemoryApprovalStore _inner = new();

        public ApprovalStoreCapabilities Capabilities { get; } =
            capabilities ?? new ApprovalStoreCapabilities(IsDurable: false, ProvidesTrustedControlPlane: true);

        public ValueTask<ApprovalStoreCreateResult> CreateAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default) =>
            create is not null ? create(request, cancellationToken) : _inner.CreateAsync(request, cancellationToken);

        public ValueTask<ApprovalStoreReadResult> ReadAsync(
            ApprovalRequestId requestId,
            CancellationToken cancellationToken = default) =>
            read is not null ? read(requestId, cancellationToken) : _inner.ReadAsync(requestId, cancellationToken);

        public ValueTask<ApprovalStoreResolveResult> ResolveAsync(
            ApprovalResponse response,
            CancellationToken cancellationToken = default) =>
            resolve is not null ? resolve(response, cancellationToken) : _inner.ResolveAsync(response, cancellationToken);

        /// <summary>Invokes the default in-memory read behavior, bypassing any overriding delegate.</summary>
        public ValueTask<ApprovalStoreReadResult> PassthroughReadAsync(
            ApprovalRequestId requestId,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(requestId, cancellationToken);
    }

    private sealed class ThrowingHandler(Exception exception): IApprovalHandler
    {
        public ValueTask<ApprovalHandlerResult> TryResolveAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    /// <summary>An approval handler that waits for its cancellation token, as a real "wait for a human" handler would.</summary>
    private sealed class HangingHandler: IApprovalHandler
    {
        public async ValueTask<ApprovalHandlerResult> TryResolveAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable: the delay above never completes without cancellation.");
        }
    }

    private sealed class ThrowingResponderAuthorizer(Exception exception): IApprovalResponderAuthorizer
    {
        public ValueTask<ApprovalResponderAuthorizationResult> AuthorizeAsync(
            ApprovalRequest request,
            ApprovalResponse candidateResponse,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class ThrowingAuditDispatcher(Exception exception): ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }
}
