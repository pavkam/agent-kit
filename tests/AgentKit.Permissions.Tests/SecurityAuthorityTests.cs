// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

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
        var authority = CreateAuthority(
            [new StubPolicy(SecurityPolicyResultKind.Deny), new StubPolicy(SecurityPolicyResultKind.Allow)],
            store,
            clock);

        var decision = await authority.AuthorizeAsync(CreateRequest(), TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("test.deny");
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
    public async Task AuthorizeAsync_WhenRequestWasMutatedToUndefinedEffect_ThrowsBeforePolicy()
    {
        var policy = new StubPolicy(SecurityPolicyResultKind.Allow);
        var authority = CreateAuthority([policy]);
        var request = CreateRequest() with { Effect = (SecurityEffect) int.MaxValue };

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request.Effect");
        policy.CallCount.ShouldBe(0);
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
                SecurityPolicyResultKind.Deny => new SecurityPolicyResult(result, "test.deny", "Denied by test policy."),
                _ => throw new InvalidOperationException(),
            });
        }
    }

    private sealed class StubGrantIdGenerator: IIdentifierGenerator<GrantId>
    {
        public GrantId Create() => new(Guid.Parse("70000000-0000-0000-0000-000000000007"));
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
