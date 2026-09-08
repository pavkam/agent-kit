// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

public sealed class SessionStoreReceiptValidationTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 14, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("intent")]
    [InlineData("grant")]
    [InlineData("request")]
    [InlineData("fence")]
    [InlineData("fingerprint")]
    [InlineData("enforcement")]
    public async Task CreateAsync_WhenGrantStoreReturnsWrongReceipt_RejectsBeforeAuditOrMutation(
        string corruptedField)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants)
        {
            IntentResultInterceptor = (result, _, _) => result.IntentReceipt is { } receipt
                ? new GrantConsumptionResult(
                    result.Status, result.RemainingUses, result.SafeMessage, Corrupt(receipt, corruptedField))
                : result,
        };
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        using var activities = CreateActivityCollector(lower.Address.AgentId);

        var rejected = await store.CreateAsync(wrapper, TestContext.Current.CancellationToken);

        _ = rejected.ShouldBeOfType<SessionCreateFailed>();
        audit.Calls.ShouldBe(0);
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("rejected");

        grants.IntentResultInterceptor = null;
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenActualGrantStoreReturnsReconciledReceipt_DoesNotRepeatAccess()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);

        var created = await store.CreateAsync(wrapper, TestContext.Current.CancellationToken);
        var replay = await store.CreateAsync(wrapper, TestContext.Current.CancellationToken);

        _ = created.ShouldBeOfType<SessionCreated>();
        _ = replay.ShouldBeOfType<SessionCreateFailed>();
        audit.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_WhenGrantStoreCancelsThenReturnsDenialOrWrongReceipt_PropagatesCancellationBeforeAccess(
        bool wrongReceipt)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        using var cancellation = new CancellationTokenSource();
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants)
        {
            IntentResultInterceptor = (result, _, _) =>
            {
                cancellation.Cancel();
                if (!wrongReceipt)
                {
                    return new GrantConsumptionResult(
                        GrantConsumptionStatus.Mismatch, result.RemainingUses, "The enforcement evidence differs.");
                }

                var receipt = result.IntentReceipt.ShouldNotBeNull();
                return new GrantConsumptionResult(
                    result.Status, result.RemainingUses, result.SafeMessage, Corrupt(receipt, "intent"));
            },
        };
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.CreateAsync(wrapper, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(0);

        grants.IntentResultInterceptor = null;
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenGrantStoreCancelsThenReturnsReconciledReceipt_PropagatesCancellationWithoutRepeatAccess()
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants);
        var audit = new RecordingSecurityAuditDispatcher();
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(wrapper, TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();
        using var cancellation = new CancellationTokenSource();
        grants.IntentResultInterceptor = (result, _, _) =>
        {
            result.Status.ShouldBe(GrantConsumptionStatus.Reconciled);
            cancellation.Cancel();
            return result;
        };

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.CreateAsync(wrapper, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_WhenAuditCancelsThenReturnsNonSuccess_PropagatesCancellationBeforeAccess(
        bool failed)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var grants = new InMemorySecurityGrantStore(timeProvider);
        using var cancellation = new CancellationTokenSource();
        var audit = new RecordingSecurityAuditDispatcher
        {
            BeforeAccepted = cancellation.Cancel,
            NextResult = failed
                ? new SecurityAuditFailed("The required audit sink failed.")
                : new SecurityAuditUnavailable("The required audit sink is unavailable."),
        };
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.CreateAsync(wrapper, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(1);

        audit.BeforeAccepted = null;
        audit.NextResult = new SecurityAuditAccepted();
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_WhenCallerCancelsAfterConsumptionOrAudit_DoesNotAccessState(
        bool cancelAfterAudit)
    {
        var timeProvider = new FakeTimeProvider(_now);
        var authoritativeGrants = new InMemorySecurityGrantStore(timeProvider);
        var grants = new InterceptingSecurityGrantStore(authoritativeGrants);
        var audit = new RecordingSecurityAuditDispatcher();
        using var cancellation = new CancellationTokenSource();
        if (cancelAfterAudit)
        {
            audit.BeforeAccepted = cancellation.Cancel;
        }
        else
        {
            grants.IntentResultInterceptor = (result, _, _) =>
            {
                if (result.Status == GrantConsumptionStatus.Consumed)
                {
                    cancellation.Cancel();
                }

                return result;
            };
        }
        var store = CreateStore(grants, audit, timeProvider);
        var lower = new TestSecurityHarness().Lower(TestFactory.CreateRequest());
        var wrapper = await AuthorizeCreateAsync(grants, store, lower);

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.CreateAsync(wrapper, cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        audit.Calls.ShouldBe(cancelAfterAudit ? 1 : 0);

        grants.IntentResultInterceptor = null;
        audit.BeforeAccepted = null;
        var retry = await AuthorizeCreateAsync(grants, store, lower);
        _ = (await store.CreateAsync(retry, TestContext.Current.CancellationToken))
            .ShouldBeOfType<SessionCreated>();
        audit.Calls.ShouldBe(cancelAfterAudit ? 2 : 1);
    }

    private static InMemorySessionStore CreateStore(
        ISecurityGrantStore grants,
        ISecurityAuditDispatcher audit,
        TimeProvider timeProvider,
        ILogger<InMemorySessionStore>? logger = null)
    {
        Debug.Assert(grants is not null, "A grant store is required.");
        Debug.Assert(audit is not null, "An audit dispatcher is required.");
        Debug.Assert(timeProvider is not null, "A deterministic clock is required.");
        return new InMemorySessionStore(
            new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)),
            new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)),
            audit,
            grants,
            timeProvider,
            logger);
    }

    private static async ValueTask<AuthorizedSessionStoreRequest<SessionStoreCreateRequest>> AuthorizeCreateAsync(
        ISecurityGrantStore grants,
        InMemorySessionStore store,
        SessionStoreCreateRequest request)
    {
        Debug.Assert(grants is not null, "A grant store is required.");
        Debug.Assert(store is not null, "A session store is required.");
        Debug.Assert(request is not null, "A lower session creation request is required.");
        var context = request.Context;
        var grant = new SecurityGrant(
            new GrantId(Guid.NewGuid()),
            new SecurityRequestId(Guid.NewGuid()),
            context.Authorization.Scope,
            context.Identity,
            context.Authorization,
            store.SecurityAudience,
            SecurityOperationKind.StateMutation,
            SecurityEffect.Create,
            [SessionStoreSecurityBinding.Resource(store.Descriptor.Key, request.Address)],
            SessionStoreSecurityBinding.Fingerprint(request),
            context.Authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            _now.AddMinutes(-1),
            _now.AddMinutes(5),
            1);
        await grants.RegisterAsync(grant, TestContext.Current.CancellationToken);
        return new AuthorizedSessionStoreRequest<SessionStoreCreateRequest>(
            request,
            store.Descriptor.Key,
            grant,
            new SecurityEnforcementIntent(new SecurityEnforcementIntentId(Guid.NewGuid()), null));
    }

    private static SecurityEnforcementIntentReceipt Corrupt(
        SecurityEnforcementIntentReceipt receipt,
        string field)
    {
        Debug.Assert(receipt is not null, "A fresh grant-store receipt is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(field), "A selected corruption axis is required.");
        var enforcement = field == "enforcement"
            ? new SecurityEnforcementRequest(
                receipt.Enforcement.Scope,
                receipt.Enforcement.Identity,
                receipt.Enforcement.Authorization!,
                receipt.Enforcement.Audience,
                receipt.Enforcement.Kind,
                receipt.Enforcement.Effect,
                receipt.Enforcement.Resources,
                new InputFingerprint("sha256:wrong-enforcement"),
                receipt.Enforcement.RevocationVersion)
            : receipt.Enforcement;
        return new SecurityEnforcementIntentReceipt(
            field == "intent" ? new SecurityEnforcementIntentId(Guid.NewGuid()) : receipt.IntentId,
            field == "grant" ? new GrantId(Guid.NewGuid()) : receipt.GrantId,
            field == "request" ? new SecurityRequestId(Guid.NewGuid()) : receipt.RequestId,
            enforcement,
            field == "fence" ? new FencingToken(1) : receipt.RequiredFence,
            field == "fingerprint" ? new ContentHash("sha256:wrong-effect") : receipt.EffectFingerprint,
            receipt.ConsumedAt);
    }

    private static ActivityCollector CreateActivityCollector(AgentId agentId)
    {
        Debug.Assert(agentId != default, "A non-default agent identity is required.");
        return new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            activity => activity.OperationName == AgentKitActivityNames.SessionStoreOperation
                && activity.GetTagItem(AgentKitTagNames.AgentId)?.Equals(agentId.ToString()) == true
                && activity.GetTagItem(AgentKitTagNames.SessionOperation)?.Equals("create") == true);
    }
}
