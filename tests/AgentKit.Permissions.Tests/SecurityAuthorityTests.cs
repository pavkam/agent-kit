// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class SecurityAuthorityTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthorizeAsync_WhenNoPolicyAllows_DeniesFailClosed()
    {
        var authority = CreateAuthority([]);
        var request = CreateRequest();

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.no_policy");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowPolicyMatches_IssuesConsumableExactGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new InMemorySecurityGrantStore(clock);
        var authority = CreateAuthority([new StubPolicy(SecurityPolicyResultKind.Allow)], store, clock);
        var request = CreateRequest();

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var allowed = decision.ShouldBeOfType<SecurityAllowed>();
        allowed.Grant.RequestId.ShouldBe(request.Id);
        allowed.Grant.Resources.ShouldBe(request.Resources);
        var consumed = await store.ValidateAndConsumeAsync(
            allowed.Grant,
            new SecurityEnforcementRequest(
                request.Scope,
                request.Identity,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                allowed.Grant.RevocationVersion),
            TestContext.Current.CancellationToken);
        consumed.Status.ShouldBe(GrantConsumptionStatus.Consumed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAnyPolicyDenies_DenyOverridesAllow()
    {
        var authority = CreateAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Allow), new StubPolicy(SecurityPolicyResultKind.Deny)]);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("test.deny");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenHardDenyPrecedesAllow_DeniesWithoutRegisteringGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var deny = new StubPolicy(SecurityPolicyResultKind.Deny);
        var allow = new StubPolicy(SecurityPolicyResultKind.Allow);
        var authority = CreateAuthority(
            [deny, allow],
            store,
            clock);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("test.deny");
        deny.CallCount.ShouldBe(1);
        allow.CallCount.ShouldBe(1);
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowAndAbstainPoliciesMatch_IssuesGrant()
    {
        var authority = CreateAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Abstain), new StubPolicy(SecurityPolicyResultKind.Allow)]);

        var request = CreateRequest();
        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityAllowed>().Grant.RequestId.ShouldBe(request.Id);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenApprovalConditionIsSatisfied_RechecksAndRegistersBoundGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var broker = new ApprovingBroker();
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            store,
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            broker,
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher());

        var request = CreateRequest();
        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var allowed = decision.ShouldBeOfType<SecurityAllowed>();
        allowed.Grant.RequestId.ShouldBe(request.Id);
        allowed.Grant.InputFingerprint.ShouldBe(request.InputFingerprint);
        broker.CallCount.ShouldBe(1);
        store.RegisterCount.ShouldBe(1);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenApprovalTakesTime_GrantDoesNotOutliveApprovedBinding()
    {
        // ApprovalScopeBinding.ExpiresAt is documented as "the exclusive approval and grant expiry"; the human approved
        // that bound. Re-basing the grant lifetime on the post-approval clock silently widens what was approved.
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var broker = new SlowApprovingBroker(clock, TimeSpan.FromMinutes(3));
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            store,
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions { MaximumGrantLifetime = TimeSpan.FromMinutes(5) }),
            broker,
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher());
        var request = CreateRequest() with { Deadline = _now.AddHours(1) };

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var allowed = decision.ShouldBeOfType<SecurityAllowed>();
        var approvedBinding = broker.LastRequest.ShouldNotBeNull().Binding;
        allowed.Grant.ExpiresAt.ShouldBeLessThanOrEqualTo(approvedBinding.ExpiresAt);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenBrokerIsUnavailable_DeniesWithACodeDistinctFromHumanDenial()
    {
        // Infrastructure unavailability and an explicit human "no" are different facts; audit must not conflate them.
        var clock = new FakeTimeProvider(_now);
        var unavailableAuthority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new FixedBroker(new ApprovalBrokerUnavailable("store offline")),
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher());
        var deniedAuthority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new DenyingBroker(),
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher());

        var unavailable = await unavailableAuthority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);
        var denied = await deniedAuthority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var unavailableCode = unavailable.ShouldBeOfType<SecurityDenied>().Denial.Code;
        var deniedCode = denied.ShouldBeOfType<SecurityDenied>().Denial.Code;
        unavailableCode.ShouldNotBe(deniedCode);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllPoliciesAbstain_DeniesFailClosed()
    {
        var authority = CreateAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Abstain), new StubPolicy(SecurityPolicyResultKind.Abstain)]);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.no_policy");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenPolicyEvaluationFails_DeniesWithoutRegisteringGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = CreateAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Allow), new ThrowingPolicy()],
            store,
            clock);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<SecurityDenied>();
        denied.Denial.Code.ShouldBe("security.policy_evaluation_failed");
        denied.Denial.SafeMessage.ShouldNotContain("policy failure");
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenPolicyReturnsInvalidContribution_DeniesWithoutRegisteringGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = CreateAuthority([new InvalidPolicy()], store, clock);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.policy_evaluation_failed");
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenPolicyCancels_PropagatesCancellationWithoutRegisteringGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = CreateAuthority([new CancellingPolicy()], store, clock);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken));

        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenDeadlineExpired_DoesNotEvaluatePolicy()
    {
        var policy = new StubPolicy(SecurityPolicyResultKind.Allow);
        var authority = CreateAuthority([policy]);
        var request = CreateRequest() with { Deadline = _now };

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.deadline_expired");
        policy.CallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthorizeAsync_WhenCapturedPolicySnapshotDiffers_DeniesBeforePolicyOrGrant(bool changeId)
    {
        var policy = new StubPolicy(SecurityPolicyResultKind.Allow);
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var evaluated = PolicySnapshot("10000000-0000-0000-0000-000000000001", "sha256:evaluated");
        var supplied = changeId
            ? PolicySnapshot("10000000-0000-0000-0000-000000000002", "sha256:evaluated")
            : PolicySnapshot("10000000-0000-0000-0000-000000000001", "sha256:changed");
        var options = new AgentPermissionOptions { PolicySnapshot = evaluated };
        var authority = CreateAuthority([policy], store, clock, options);
        var request = CreateCapturedRequest(supplied);

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.captured_context_mismatch");
        policy.CallCount.ShouldBe(0);
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenOptionsMutateAfterConstruction_RetainsOriginalPolicySnapshotBinding()
    {
        var original = PolicySnapshot("10000000-0000-0000-0000-000000000001", "sha256:original");
        var changed = PolicySnapshot("10000000-0000-0000-0000-000000000002", "sha256:changed");
        var options = new AgentPermissionOptions { PolicySnapshot = original };
        var policy = new StubPolicy(SecurityPolicyResultKind.Allow);
        var authority = CreateAuthority([policy], options: options);
        options.PolicySnapshot = changed;
        options.PolicyVersion = 2;

        var originalDecision = await authority.AuthorizeAsync(
            CreateCapturedRequest(original), TestContext.Current.CancellationToken);
        var changedDecision = await authority.AuthorizeAsync(
            CreateCapturedRequest(changed), TestContext.Current.CancellationToken);

        _ = originalDecision.ShouldBeOfType<SecurityAllowed>();
        changedDecision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.captured_context_mismatch");
        policy.CallCount.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WhenPolicySnapshotVersionDiffers_ThrowsBeforeRetainingOptions()
    {
        var options = new AgentPermissionOptions
        {
            PolicyVersion = 2,
            PolicySnapshot = PolicySnapshot("10000000-0000-0000-0000-000000000001", "sha256:original"),
        };

        var exception = Should.Throw<ArgumentException>(() => CreateAuthority([], options: options));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public void SecurityRequest_WhenMutatedToUndefinedEffect_ThrowsBeforeAuthorization()
    {
        // SecurityRequest.Effect now validates in its own init accessor (closing the with-expression
        // gap the type used to have), so an undefined value can no longer reach AuthorizeAsync at
        // all; SecurityAuthority.AuthorizeAsync's own ThrowIfUndefined(request.Effect) guard remains
        // as defense in depth but is no longer reachable through this exact mutation path.
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => CreateRequest() with { Effect = (SecurityEffect) int.MaxValue });

        exception.ParamName.ShouldBe("Effect");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenApprovalRequiredWithoutCoordination_DeniesAsApprovalUnavailable()
    {
        var authority = CreateAuthority([new StubPolicy(SecurityPolicyResultKind.RequireApproval)]);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.approval_unavailable");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenApprovalExpires_DeniesWithApprovalExpiredCode()
    {
        var clock = new FakeTimeProvider(_now);
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new FixedBroker(new ApprovalBrokerExpired()),
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher());

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.approval_expired");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenApprovalResponseIdentifiesADifferentRequest_DeniesAsStale()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            store,
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new MismatchedRequestIdBroker(),
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher());

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.approval_stale");
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenRequiredApprovalAuditThrowsOperationCanceled_Propagates()
    {
        var clock = new FakeTimeProvider(_now);
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new ApprovingBroker(),
            new StubApprovalRequestIdGenerator(),
            new ThrowingAuditDispatcher(new OperationCanceledException()));

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthorizeAsync_WhenRequiredApprovalAuditThrowsException_DeniesAsAuditUnavailable()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            store,
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new ApprovingBroker(),
            new StubApprovalRequestIdGenerator(),
            new ThrowingAuditDispatcher(new InvalidOperationException("boom")));

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<SecurityDenied>();
        denied.Denial.Code.ShouldBe("security.audit_unavailable");
        denied.Denial.SafeMessage.ShouldBe("Required security audit failed.");
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenRequiredApprovalAuditIsNotAccepted_DeniesAsAuditUnavailable()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            store,
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new ApprovingBroker(),
            new StubApprovalRequestIdGenerator(),
            new RejectingAuditDispatcher());

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<SecurityDenied>();
        denied.Denial.Code.ShouldBe("security.audit_unavailable");
        denied.Denial.SafeMessage.ShouldBe("Required security audit was not accepted.");
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenOrdinaryAllowRegistersAGrantAndAnAuditDispatcherIsConfigured_DispatchesGrantIssued()
    {
        // Every registered grant must be audited when a dispatcher is configured, not only the ones
        // a human approved: the ordinary allow path (no approval involved at all) previously
        // registered a grant with no audit record whatsoever.
        var clock = new FakeTimeProvider(_now);
        var dispatcher = new RecordingAuditDispatcher();
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Allow)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new FaultingBroker(),
            new StubApprovalRequestIdGenerator(),
            dispatcher);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var allowed = decision.ShouldBeOfType<SecurityAllowed>();
        var audit = dispatcher.Dispatched.ShouldHaveSingleItem();
        audit.EventKind.ShouldBe(SecurityAuditEventKind.GrantIssued);
        audit.Outcome.ShouldBe(SecurityAuditOutcome.Accepted);
        audit.GrantId.ShouldBe(allowed.Grant.Id);
        audit.ApprovalRequestId.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenOrdinaryAllowAuditIsNotAccepted_DeniesAsAuditUnavailableWithoutRegisteringTheGrant()
    {
        var clock = new FakeTimeProvider(_now);
        var store = new RecordingGrantStore(new InMemorySecurityGrantStore(clock));
        var authority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Allow)],
            store,
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new FaultingBroker(),
            new StubApprovalRequestIdGenerator(),
            new RejectingAuditDispatcher());

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        var denied = decision.ShouldBeOfType<SecurityDenied>();
        denied.Denial.Code.ShouldBe("security.audit_unavailable");
        store.RegisterCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCallerTokenCancelsDuringPolicyEvaluation_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var authority = CreateAuthority([new CancelingExternalPolicy(cts)]);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await authority.AuthorizeAsync(CreateRequest(), cts.Token));
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowedDeniedCancelledOrFaulted_LogsTheirDistinctEvents()
    {
        var clock = new FakeTimeProvider(_now);
        var logger = new RecordingLogger();
        var allowingAuthority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Allow)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            logger);
        var denyingAuthority = new SecurityAuthority(
            [],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            logger);
        using var cts = new CancellationTokenSource();
        var cancellingAuthority = new SecurityAuthority(
            [new CancelingExternalPolicy(cts)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            logger);
        var faultingAuthority = new SecurityAuthority(
            [new StubPolicy(SecurityPolicyResultKind.RequireApproval)],
            new InMemorySecurityGrantStore(clock),
            new StubGrantIdGenerator(),
            clock,
            Options.Create(new AgentPermissionOptions()),
            new FaultingBroker(),
            new StubApprovalRequestIdGenerator(),
            new AcceptingAuditDispatcher(),
            logger);

        _ = await allowingAuthority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);
        _ = await denyingAuthority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await cancellingAuthority.AuthorizeAsync(CreateRequest(), cts.Token));
        // The broker's unguarded fault escapes AuthorizeCoreAsync into AuthorizeAsync's own catch(Exception), which
        // is the only path that reaches AuthorizationFaulted from a validated request.
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await faultingAuthority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken));

        logger.EventIds.ShouldContain(5000);
        logger.EventIds.ShouldContain(5001);
        logger.EventIds.ShouldContain(5002);
        logger.EventIds.ShouldContain(5003);
        logger.EventIds.ShouldContain(5004);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenDenied_EmitsSuccessfulDecisionActivityWithoutResources()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var authority = CreateAuthority([]);
        var request = CreateRequest();

        _ = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.SecurityAuthorize);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("denied");
        activity.GetTagItem(AgentKitTagNames.SecurityRequestId).ShouldBe(request.Id.ToString());
        activity.TagObjects.Select(static tag => tag.Value).ShouldNotContain(request.Resources[0]);
    }

    private static SecurityAuthority CreateAuthority(
        IEnumerable<ISecurityPolicy> policies,
        ISecurityGrantStore? store = null,
        TimeProvider? timeProvider = null,
        AgentPermissionOptions? options = null) => new(
            policies,
            store ?? new InMemorySecurityGrantStore(timeProvider ?? new FakeTimeProvider(_now)),
            new StubGrantIdGenerator(),
            timeProvider ?? new FakeTimeProvider(_now),
            Options.Create(options ?? new AgentPermissionOptions()));

    private static SecurityPolicySnapshotReference PolicySnapshot(string id, string fingerprint) => new(
        new SecurityPolicySnapshotId(Guid.Parse(id)),
        new SecurityPolicyVersion(1),
        new ContentHash(fingerprint));

    private static SecurityRequest CreateCapturedRequest(SecurityPolicySnapshotReference snapshot)
    {
        var legacy = CreateRequest();
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"), new SecurityProfileVersion(1), snapshot,
            new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), legacy.Scope, legacy.Identity);
        return new SecurityRequest(
            legacy.Id, legacy.Scope, legacy.ToolCallId, legacy.Identity, authorization, legacy.Audience,
            legacy.Kind, legacy.Effect, legacy.Resources, legacy.InputFingerprint, legacy.Deadline);
    }

    private static SecurityRequest CreateRequest() => SecurityAuthorityTestData.CreateRequest(_now);

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;

    private sealed class StubPolicy(SecurityPolicyResultKind result): ISecurityPolicy
    {
        public int CallCount { get; private set; }

        public ValueTask<SecurityPolicyResult> EvaluateAsync(
            SecurityRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result switch
            {
                SecurityPolicyResultKind.Abstain => new SecurityPolicyResult(result, null, null),
                SecurityPolicyResultKind.Allow => new SecurityPolicyResult(result, "test.allow", "Allowed by test policy."),
                SecurityPolicyResultKind.RequireApproval => new SecurityPolicyResult(result, "test.approval", "Approval required by test policy."),
                SecurityPolicyResultKind.Deny => new SecurityPolicyResult(result, "test.deny", "Denied by test policy."),
                _ => throw new InvalidOperationException(),
            });
        }
    }

    private sealed class StubGrantIdGenerator: IIdentifierGenerator<GrantId>
    {
        public GrantId Create() => new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
    }

    private sealed class StubApprovalRequestIdGenerator: IIdentifierGenerator<ApprovalRequestId>
    {
        public ApprovalRequestId Create() =>
            new(Guid.Parse("80000000-0000-0000-0000-000000000008"));
    }

    private sealed class ApprovingBroker: IApprovalBroker
    {
        public int CallCount { get; private set; }

        public ValueTask<ApprovalBrokerResult> RequestAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            var response = new ApprovalResponse(
                new ApprovalResponseId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                request.Id,
                request.Binding,
                ApprovalResolution.Approved,
                request.Binding.Request.Identity,
                request.CreatedAt.AddSeconds(1));
            return ValueTask.FromResult<ApprovalBrokerResult>(new ApprovalBrokerApproved(response));
        }
    }

    private sealed class SlowApprovingBroker(FakeTimeProvider clock, TimeSpan thinkTime): IApprovalBroker
    {
        public ApprovalRequest? LastRequest { get; private set; }

        public ValueTask<ApprovalBrokerResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            clock.Advance(thinkTime);
            var response = new ApprovalResponse(
                new ApprovalResponseId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                request.Id,
                request.Binding,
                ApprovalResolution.Approved,
                request.Binding.Request.Identity,
                clock.GetUtcNow());
            return ValueTask.FromResult<ApprovalBrokerResult>(new ApprovalBrokerApproved(response));
        }
    }

    private sealed class FixedBroker(ApprovalBrokerResult result): IApprovalBroker
    {
        public ValueTask<ApprovalBrokerResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(result);
    }

    /// <summary>A broker whose unguarded fault escapes directly to the authority's outer catch.</summary>
    private sealed class FaultingBroker: IApprovalBroker
    {
        public ValueTask<ApprovalBrokerResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("broker failed");
    }

    private sealed class RecordingLogger: ILogger<SecurityAuthority>
    {
        public List<int> EventIds { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            EventIds.Add(eventId.Id);
    }

    private sealed class DenyingBroker: IApprovalBroker
    {
        public ValueTask<ApprovalBrokerResult> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        {
            var response = new ApprovalResponse(
                new ApprovalResponseId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                request.Id,
                request.Binding,
                ApprovalResolution.Denied,
                request.Binding.Request.Identity,
                request.CreatedAt.AddSeconds(1));
            return ValueTask.FromResult<ApprovalBrokerResult>(new ApprovalBrokerDenied(response));
        }
    }

    private sealed class RecordingAuditDispatcher: ISecurityAuditDispatcher
    {
        public List<SecurityAuditRecord> Dispatched { get; } = [];

        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dispatched.Add(record);
            return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
        }
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
        }
    }

    private sealed class ThrowingAuditDispatcher(Exception exception): ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class RejectingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(
                new SecurityAuditUnavailable("No compatible durable security audit sink is available."));
    }

    /// <summary>An approving broker whose response identifies a different approval request than the one asked about.</summary>
    private sealed class MismatchedRequestIdBroker: IApprovalBroker
    {
        public ValueTask<ApprovalBrokerResult> RequestAsync(
            ApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = new ApprovalResponse(
                new ApprovalResponseId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                new ApprovalRequestId(Guid.Parse("9a000000-0000-0000-0000-00000000000a")),
                request.Binding,
                ApprovalResolution.Approved,
                request.Binding.Request.Identity,
                request.CreatedAt.AddSeconds(1));
            return ValueTask.FromResult<ApprovalBrokerResult>(new ApprovalBrokerApproved(response));
        }
    }

    /// <summary>Cancels an externally observed token and rethrows using the exact same token the authority passed in.</summary>
    private sealed class CancelingExternalPolicy(CancellationTokenSource externalCancellation): ISecurityPolicy
    {
        public ValueTask<SecurityPolicyResult> EvaluateAsync(
            SecurityRequest request,
            CancellationToken cancellationToken = default)
        {
            externalCancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "test.allow", "Allowed by test policy."));
        }
    }

    private sealed class ThrowingPolicy: ISecurityPolicy
    {
        public ValueTask<SecurityPolicyResult> EvaluateAsync(
            SecurityRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("policy failure containing protected content");
    }

    private sealed class InvalidPolicy: ISecurityPolicy
    {
        public ValueTask<SecurityPolicyResult> EvaluateAsync(
            SecurityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                new SecurityPolicyResult(SecurityPolicyResultKind.Allow, "test.allow", "Allowed by test policy.")
                {
                    Kind = (SecurityPolicyResultKind) int.MaxValue,
                });
    }

    private sealed class CancellingPolicy: ISecurityPolicy
    {
        public ValueTask<SecurityPolicyResult> EvaluateAsync(
            SecurityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromCanceled<SecurityPolicyResult>(new CancellationToken(canceled: true));
    }

    private sealed class RecordingGrantStore(ISecurityGrantStore inner): ISecurityGrantStore
    {
        public int RegisterCount { get; private set; }

        public async ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
        {
            RegisterCount++;
            await inner.RegisterAsync(grant, cancellationToken);
        }

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            CancellationToken cancellationToken = default) =>
            inner.ValidateAndConsumeAsync(grant, enforcement, cancellationToken);

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
            inner.RevokeAsync(grantId, cancellationToken);
    }
}
