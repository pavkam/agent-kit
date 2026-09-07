// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;

public sealed class SessionPlanStateStoreTests
{
    [Fact]
    public async Task ReadAsync_WhenGrantMatches_ConsumesBeforeSessionObservation()
    {
        var sessions = SessionsWith(TestData.Entry());
        var grants = new RecordingGrantStore();
        var store = Store(sessions, grants);
        var fingerprint = PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress());

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, fingerprint)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<PlanStateFound>().Plan.Revision.ShouldBe(new PlanRevision(1));
        grants.Enforcements.ShouldHaveSingleItem().InputFingerprint.ShouldBe(fingerprint);
        sessions.LoadCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ReadAsync_WhenGrantMismatches_PerformsNoSessionObservation()
    {
        var sessions = SessionsWith(TestData.Entry());
        var store = Store(sessions, new RecordingGrantStore());
        var wrong = new InputFingerprint("wrong");

        var result = await store.ReadAsync(
            new PlanReadRequest(TestData.Context, TestData.ToolCallId, TestData.Grant(
                store.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe, wrong)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<PlanStateDenied>();
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
        ISecurityGrantStore grants) => new(
            sessions,
            grants,
            new FixedPlanIdGenerator(),
            new FixedEntryIdGenerator(),
            new FixedTimeProvider());

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
