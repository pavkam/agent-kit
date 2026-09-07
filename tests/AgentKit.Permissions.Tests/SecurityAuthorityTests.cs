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
    public async Task AuthorizeAsync_WhenDeadlineExpired_DoesNotEvaluatePolicy()
    {
        var policy = new StubPolicy(SecurityPolicyResultKind.Allow);
        var authority = CreateAuthority([policy]);
        var request = CreateRequest() with { Deadline = _now };

        var decision = await authority.AuthorizeAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<SecurityDenied>().Denial.Code.ShouldBe("security.deadline_expired");
        policy.CallCount.ShouldBe(0);
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
        TimeProvider? timeProvider = null) => new(
            policies,
            store ?? new InMemorySecurityGrantStore(timeProvider ?? new FakeTimeProvider(_now)),
            new StubGrantIdGenerator(),
            timeProvider ?? new FakeTimeProvider(_now),
            Options.Create(new AgentPermissionOptions()));

    private static SecurityRequest CreateRequest()
    {
        var grant = InMemorySecurityGrantStoreTests.CreateGrantForTests();
        return new SecurityRequest(
            grant.RequestId,
            grant.Scope,
            null,
            grant.Identity,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            _now.AddMinutes(10));
    }

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
}
