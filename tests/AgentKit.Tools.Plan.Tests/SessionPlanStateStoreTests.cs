// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;

using AgentKit.Permissions;

public sealed class SessionPlanStateStoreTests
{
    [Fact]
    public void Constructor_WhenIntentIdsIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionPlanStateStore(
            SessionsWith(),
            new RecordingGrantStore(),
            new FixedPlanIdGenerator(),
            new FixedEntryIdGenerator(),
            null!,
            new FixedTimeProvider()));

        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task ReadAsync_WhenGrantMatches_ConsumesBeforeSessionObservation()
    {
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore();
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<PlanStateFound>().Plan.Revision.ShouldBe(new PlanRevision(1));
        grants.Enforcements.ShouldHaveSingleItem().InputFingerprint.ShouldBe(fingerprint);
        sessions.LoadCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_WithRealGrantStoreAndCapturedGrant_ConsumesBeforeSessionObservation()
    {
        var sessions = SessionsWith(TestData.Entry());
        var grants = new InMemorySecurityGrantStore(new FixedTimeProvider());
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());
        var grant = TestData.Grant(
            store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint);
        await grants.RegisterAsync(grant, TestContext.Current.CancellationToken);

        var result = await store.ReadAsync(
            new PlanReadRequest(
                TestData.Context, TestData.SessionProfile, TestData.ToolCallId, grant),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<PlanStateFound>();
        sessions.LoadCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_WhenGrantMismatches_PerformsNoSessionObservation()
    {
        var sessions = SessionsWith(TestData.Entry());
        var store = Store(sessions, new RecordingGrantStore());
        var wrong = new InputFingerprint("wrong");

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, wrong)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<PlanStateDenied>();
        sessions.LoadCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Reconciled, false)]
    [InlineData(GrantConsumptionStatus.Consumed, true)]
    public async Task ReadAsync_WhenReceiptDoesNotProveFreshExactConsumption_PerformsNoSessionObservation(
        GrantConsumptionStatus status,
        bool wrongReceipt)
    {
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore { Status = status, ReturnWrongReceipt = wrongReceipt };
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<PlanStateDenied>();
        sessions.LoadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ReadAsync_WhenReceiptReconstructsExactEnforcement_AcceptsValueEqualResources()
    {
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore
        {
            ReceiptEnforcement = Reconstruct,
        };
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<PlanStateFound>();
        var enforcement = grants.Enforcements.ShouldHaveSingleItem();
        var reconstructed = Reconstruct(enforcement);
        reconstructed.ShouldNotBeSameAs(enforcement);
        reconstructed.Resources.Equals(enforcement.Resources).ShouldBeFalse();
        reconstructed.Resources.SequenceEqual(enforcement.Resources).ShouldBeTrue();
        sessions.LoadCalls.ShouldBe(1);

        static SecurityEnforcementRequest Reconstruct(SecurityEnforcementRequest expected) =>
            ReconstructEnforcement(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_WhenReceiptChangesResourcesOrAuthorization_PerformsNoSessionObservation(
        bool alterResources)
    {
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore
        {
            ReceiptEnforcement = expected => alterResources
                ? ReconstructEnforcement(expected, resources: [new ProtectedResource(
                    ProtectedResourceKind.ApplicationState,
                    "plan:session/altered")])
                : ReconstructEnforcement(expected, authorization: AlteredAuthorization(expected)),
        };
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<PlanStateDenied>();
        sessions.LoadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ReadAsync_WhenCallerAlreadyCancelled_CreatesNoIntentAndPerformsNoEffects()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore();
        var intentIds = new FixedIntentIdGenerator();
        var store = Store(sessions, grants, intentIds);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.ReadAsync(
                new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                    store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        intentIds.Calls.ShouldBe(0);
        grants.Enforcements.ShouldBeEmpty();
        grants.Intents.ShouldBeEmpty();
        sessions.LoadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ReadAsync_WhenCancelledAfterConsumption_ThrowsBeforeSessionObservation()
    {
        using var cancellation = new CancellationTokenSource();
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore { CancelBeforeReturn = cancellation };
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.ReadAsync(
                new PlanReadRequest(TestData.Context, TestData.SessionProfile, TestData.ToolCallId, TestData.Grant(
                    store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
                cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        sessions.LoadCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ReplaceAsync_WhenNoPlanExpected_AppendsFirstTypedRevision()
    {
        var sessions = SessionsWith();
        var store = Store(sessions, new RecordingGrantStore());
        var fingerprint = PlanSecurityBinding.ReplaceFingerprint(
            TestData.Context.ToAddress(), "Ship it", TestData.Items(), null);

        var result = await store.ReplaceAsync(
            new PlanReplaceRequest(
                TestData.Context,
                TestData.SessionProfile,
                TestData.ToolCallId,
                "Ship it",
                TestData.Items(),
                null,
                TestData.Grant(store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, fingerprint)),
            TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<PlanStateFound>().Plan;
        plan.Id.ShouldBe(TestData.PlanId);
        plan.Revision.ShouldBe(new PlanRevision(1));
        var append = sessions.Appends.ShouldHaveSingleItem();
        append.ExpectedVersion.ShouldBe(new SessionVersion(0));
        append.Entries.ShouldHaveSingleItem().ShouldBeOfType<PlanSessionEntry>().Plan.ShouldBe(plan);
    }

    [Fact]
    public async Task ReplaceAsync_WhenRevisionMatches_RetainsPlanIdentityAndAdvancesRevision()
    {
        var sessions = SessionsWith(TestData.Entry());
        var store = Store(sessions, new RecordingGrantStore());
        var expected = new PlanRevision(1);
        var fingerprint = PlanSecurityBinding.ReplaceFingerprint(
            TestData.Context.ToAddress(), "Changed", TestData.Items(), expected);

        var result = await store.ReplaceAsync(
            new PlanReplaceRequest(
                TestData.Context,
                TestData.SessionProfile,
                TestData.ToolCallId,
                "Changed",
                TestData.Items(),
                expected,
                TestData.Grant(store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, fingerprint)),
            TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<PlanStateFound>().Plan;
        plan.Id.ShouldBe(TestData.PlanId);
        plan.Revision.ShouldBe(new PlanRevision(2));
        sessions.Appends.ShouldHaveSingleItem().Entries.ShouldHaveSingleItem().CausalParentId.ShouldBe(TestData.Entry().Id);
    }

    [Fact]
    public async Task ReplaceAsync_WhenRevisionStale_ReturnsConflictWithoutAppend()
    {
        var sessions = SessionsWith(TestData.Entry(revision: 2));
        var store = Store(sessions, new RecordingGrantStore());
        var expected = new PlanRevision(1);
        var fingerprint = PlanSecurityBinding.ReplaceFingerprint(
            TestData.Context.ToAddress(), "Changed", TestData.Items(), expected);

        var result = await store.ReplaceAsync(
            new PlanReplaceRequest(
                TestData.Context,
                TestData.SessionProfile,
                TestData.ToolCallId,
                "Changed",
                TestData.Items(),
                expected,
                TestData.Grant(store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, fingerprint)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<PlanStateConflict>().CurrentRevision.ShouldBe(new PlanRevision(2));
        sessions.Appends.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetStatusAsync_WhenValid_AppendsCompleteSnapshotAndAdvancesRevision()
    {
        var sessions = SessionsWith(TestData.Entry(first: PlanItemStatus.InProgress));
        var store = Store(sessions, new RecordingGrantStore());
        var expected = new PlanRevision(1);
        var fingerprint = PlanSecurityBinding.StatusFingerprint(
            TestData.Context.ToAddress(), new PlanItemId("one"), PlanItemStatus.Completed, expected);

        var result = await store.SetStatusAsync(
            new PlanStatusRequest(
                TestData.Context,
                TestData.SessionProfile,
                TestData.ToolCallId,
                new PlanItemId("one"),
                PlanItemStatus.Completed,
                expected,
                TestData.Grant(store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, fingerprint)),
            TestContext.Current.CancellationToken);

        var plan = result.ShouldBeOfType<PlanStateFound>().Plan;
        plan.Revision.ShouldBe(new PlanRevision(2));
        plan.Items[0].Status.ShouldBe(PlanItemStatus.Completed);
        _ = sessions.Appends.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SetStatusAsync_WhenItWouldCreateTwoActiveItems_FailsWithoutAppend()
    {
        var sessions = SessionsWith(TestData.Entry(first: PlanItemStatus.InProgress));
        var store = Store(sessions, new RecordingGrantStore());
        var expected = new PlanRevision(1);
        var fingerprint = PlanSecurityBinding.StatusFingerprint(
            TestData.Context.ToAddress(), new PlanItemId("two"), PlanItemStatus.InProgress, expected);

        var result = await store.SetStatusAsync(
            new PlanStatusRequest(
                TestData.Context,
                TestData.SessionProfile,
                TestData.ToolCallId,
                new PlanItemId("two"),
                PlanItemStatus.InProgress,
                expected,
                TestData.Grant(store.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, fingerprint)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<PlanStateFailed>().SafeMessage.ShouldContain("At most one");
        sessions.Appends.ShouldBeEmpty();
    }

    private static SessionPlanStateStore Store(
        ISessionCoordinator sessions,
        ISecurityGrantStore grants,
        FixedIntentIdGenerator? intentIds = null) => new(
            sessions,
            grants,
            new FixedPlanIdGenerator(),
            new FixedEntryIdGenerator(),
            intentIds ?? new FixedIntentIdGenerator(),
            new FixedTimeProvider());

    private static SecurityEnforcementRequest ReconstructEnforcement(
        SecurityEnforcementRequest expected,
        ImmutableArray<ProtectedResource>? resources = null,
        SecurityAuthorizationContext? authorization = null) => new(
        expected.Scope,
        expected.Identity,
        authorization ?? expected.Authorization!,
        expected.Audience,
        expected.Kind,
        expected.Effect,
        resources ?? [.. expected.Resources],
        expected.InputFingerprint,
        expected.RevocationVersion);

    private static SecurityAuthorizationContext AlteredAuthorization(SecurityEnforcementRequest expected)
    {
        var authorization = expected.Authorization!;
        return new SecurityAuthorizationContext(
            new SecurityProfileKey("altered-security"),
            authorization.ProfileVersion,
            authorization.PolicySnapshot,
            authorization.AuthorityKey,
            authorization.AgentDefinitionRevision,
            authorization.ConfigurationVersion,
            expected.Scope,
            expected.Identity);
    }

    private static RecordingSessionCoordinator SessionsWith(params PlanSessionEntry[] entries)
    {
        var sessions = new RecordingSessionCoordinator
        {
            LoadResult = new SessionLoaded(TestData.Descriptor(new SessionVersion(entries.Length))),
        };
        sessions.Pages.Enqueue(new SessionPage([.. entries], new SessionSequence(entries.Length), false));
        return sessions;
    }
}
