// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

public sealed class DefaultSessionRunCoordinatorTests
{
    [Fact]
    public void Constructor_WhenRequiredCollaboratorIsNull_ThrowsExactArgumentNullException()
    {
        var ids = new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value));
        var options = Options.Create(new AgentSessionOptions());

        Should.Throw<ArgumentNullException>(() => new DefaultSessionRunCoordinator(null!,
            TimeProvider.System, options)).ParamName.ShouldBe("leaseIds");
        Should.Throw<ArgumentNullException>(() => new DefaultSessionRunCoordinator(ids,
            null!, options)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new DefaultSessionRunCoordinator(ids,
            TimeProvider.System, null!)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new DefaultSessionRunCoordinator(ids,
            TimeProvider.System, new NullSessionOptions())).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => new DefaultSessionRunCoordinator(ids,
            TimeProvider.System, Options.Create(new AgentSessionOptions
            {
                BusyWaitTimeout = TimeSpan.FromTicks(-1),
            }))).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => new DefaultSessionRunCoordinator(ids,
            TimeProvider.System, Options.Create(new AgentSessionOptions
            {
                BusyWaitTimeout = AgentSessionOptions.MaximumBusyWaitTimeout + TimeSpan.FromTicks(1),
            }))).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Release_WhenArgumentsAreInvalid_ThrowsExactExceptionAndParamName()
    {
        var scenario = Scenario.Create();
        var address = scenario.Request.Context.ToAddress();
        var tenantId = scenario.Request.Context.Identity.TenantId;
        var laneId = scenario.Request.ExecutionLaneId;
        var leaseId = new SessionLeaseId(Guid.NewGuid());

        var tenantException = Should.Throw<ArgumentException>(() => scenario.Coordinator.Release(default,
            address, laneId, leaseId));
        tenantException.GetType().ShouldBe(typeof(ArgumentNullException));
        tenantException.ParamName.ShouldBe("tenantId");
        var addressException = Should.Throw<ArgumentNullException>(() => scenario.Coordinator.Release(tenantId,
            null!, laneId, leaseId));
        addressException.GetType().ShouldBe(typeof(ArgumentNullException));
        addressException.ParamName.ShouldBe("address");
        var laneException = Should.Throw<ArgumentOutOfRangeException>(() => scenario.Coordinator.Release(tenantId,
            address, default, leaseId));
        laneException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        laneException.ParamName.ShouldBe("laneId");
        var leaseException = Should.Throw<ArgumentOutOfRangeException>(() => scenario.Coordinator.Release(tenantId,
            address, laneId, default));
        leaseException.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        leaseException.ParamName.ShouldBe("leaseId");
    }

    [Fact]
    public void Release_WhenNoSlotWasEverAcquired_IsANoOp()
    {
        var scenario = Scenario.Create();

        Should.NotThrow(() => scenario.Coordinator.Release(scenario.Request.Context.Identity.TenantId,
            scenario.Request.Context.ToAddress(), scenario.Request.ExecutionLaneId,
            new SessionLeaseId(Guid.NewGuid())));
    }

    [Fact]
    public async Task Release_WhenLeaseIdDoesNotMatchCurrentOwner_DoesNotFreeTheLane()
    {
        var scenario = Scenario.Create();
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);

        scenario.Coordinator.Release(scenario.Request.Context.Identity.TenantId,
            scenario.Request.Context.ToAddress(), scenario.Request.ExecutionLaneId,
            new SessionLeaseId(Guid.NewGuid()));
        var busy = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        _ = busy.ShouldBeOfType<SessionRunBusy>();
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenProfileRequiresDistributedFencing_ReturnsUnavailable()
    {
        var scenario = Scenario.Create();
        var fencingProfile = new SessionProfileSnapshot(scenario.Profile.Reference, scenario.Profile.CoordinatorKey,
            scenario.Profile.RunCoordinatorKey, scenario.Profile.DefaultStoreKey,
            scenario.Profile.RequiredStoreCapabilities, scenario.Profile.RequiresDurableStore,
            requiresDistributedFencing: true, scenario.Profile.RetentionProfile, scenario.Profile.BusyBehavior,
            scenario.Profile.MaximumAppendEntries, scenario.Profile.MaximumPageSize,
            scenario.Profile.VerifySnapshotHashes, scenario.Profile.DeleteOnDispose,
            scenario.Profile.ConfigurationFingerprint);
        var capability = new SessionExecutionCapability(fencingProfile, scenario.SessionCoordinator,
            scenario.Coordinator);

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunLeaseUnavailable>().SafeReason.ShouldBe(
            "The selected profile requires distributed fencing unavailable from the local coordinator.");
        scenario.SessionCoordinator.LoadCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReleaseAsync_WhenLoadThrowsCancellation_LogsCancelledAndStillFreesTheLaneLocally()
    {
        var scenario = Scenario.Create();
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        scenario.SessionCoordinator.OnLoad = (_, _, _) => throw new OperationCanceledException("load cancelled");

        await acquired.Lease.ReleaseAsync(TestContext.Current.CancellationToken);
        var reacquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        scenario.SessionCoordinator.ReleaseCalls.ShouldBeEmpty();
        _ = reacquired.ShouldBeOfType<SessionRunLeaseAcquired>();
        await ((SessionRunLeaseAcquired) reacquired).Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenCanonicalStateMatches_ReturnsExactLease()
    {
        var scenario = Scenario.Create();
        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        var lease = result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease;
        lease.AgentId.ShouldBe(scenario.Request.AgentId);
        lease.TenantId.ShouldBe(scenario.Request.Context.Identity.TenantId);
        lease.SessionId.ShouldBe(scenario.Request.SessionId);
        lease.ExecutionLaneId.ShouldBe(scenario.Request.ExecutionLaneId);
        lease.OperationId.ShouldBe(scenario.Request.OperationId);
        lease.RunId.ShouldBe(scenario.Request.RunId);
        lease.StateRevision.ShouldBe(scenario.Request.ExpectedStateRevision);
        lease.Fence.ShouldBeNull();
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenLeaseIdentityGenerationFails_ReleasesProvisionalSlot()
    {
        var scenario = Scenario.Create();
        var ids = new ThrowOnceLeaseIdGenerator();
        var coordinator = new DefaultSessionRunCoordinator(ids, TimeProvider.System,
            Options.Create(new AgentSessionOptions()));
        var capability = new SessionExecutionCapability(scenario.Profile,
            scenario.SessionCoordinator, coordinator);

        _ = await Should.ThrowAsync<InvalidOperationException>(coordinator
            .AcquireAsync(scenario.Request, capability, TestContext.Current.CancellationToken).AsTask());
        var result = await coordinator.AcquireAsync(scenario.Request, capability,
            TestContext.Current.CancellationToken);

        await result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenCapabilitySelectedDifferentRunCoordinator_RejectsBeforeProtectedLoad()
    {
        var scenario = Scenario.Create();
        var other = new DefaultSessionRunCoordinator(
            new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
            TimeProvider.System, Options.Create(new AgentSessionOptions()));
        var mismatched = new SessionExecutionCapability(scenario.Profile,
            scenario.SessionCoordinator, other);

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, mismatched,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunLeaseConflict>().Kind
            .ShouldBe(SessionRunLeaseConflictKind.SessionProfile);
        scenario.SessionCoordinator.LoadCount.ShouldBe(0);
    }

    [Fact]
    public async Task AcquireAsync_WhenDifferentOperationRequestsOwnedLane_ReturnsConflictAfterProtectedLoad()
    {
        var scenario = Scenario.Create();
        var first = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        var secondScenario = scenario.WithOperation();

        var result = await scenario.Coordinator.AcquireAsync(secondScenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunLeaseConflict>().Kind
            .ShouldBe(SessionRunLeaseConflictKind.AcceptedState);
        scenario.SessionCoordinator.LoadCount.ShouldBe(2);
        await first.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenProtectedBusyLookupIsDenied_ReturnsUnavailableAndKeepsOwner()
    {
        var scenario = Scenario.Create();
        var first = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(
                new SessionRunStateUnavailable("The protected lookup was denied."));

        var denied = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(scenario.State));
        var sameOwner = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        _ = denied.ShouldBeOfType<SessionRunLeaseUnavailable>();
        var busy = sameOwner.ShouldBeOfType<SessionRunBusy>();
        busy.ActiveOperationId.ShouldBe(scenario.Request.OperationId);
        busy.ActiveRunId.ShouldBe(scenario.Request.RunId);
        await first.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenSameTenantPrincipalDoesNotMatchAcceptedOwner_DoesNotDiscloseBusyOwner()
    {
        var scenario = Scenario.Create();
        var first = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);

        var denied = await scenario.Coordinator.AcquireAsync(scenario.WithPrincipal("user-2").Request,
            scenario.Capability, TestContext.Current.CancellationToken);
        var sameOwner = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        denied.ShouldBeOfType<SessionRunLeaseConflict>().Kind
            .ShouldBe(SessionRunLeaseConflictKind.AcceptedState);
        _ = sameOwner.ShouldBeOfType<SessionRunBusy>();
        await first.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenSameLaneRaces_OnlyProvisionalOwnerLoadsCanonicalState()
    {
        var scenario = Scenario.Create();
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource<SessionRunStateResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.SessionCoordinator.OnLoadRunState = async (_, _, _) =>
        {
            loadStarted.SetResult();
            return await releaseLoad.Task.ConfigureAwait(false);
        };
        var firstTask = scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken).AsTask();
        await loadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var second = await scenario.Coordinator.AcquireAsync(scenario.WithOperation().Request,
            scenario.Capability, TestContext.Current.CancellationToken);
        releaseLoad.SetResult(new SessionRunStateLoaded(scenario.State));
        var first = await firstTask;

        _ = second.ShouldBeOfType<SessionRunLeaseUnavailable>();
        scenario.SessionCoordinator.LoadCount.ShouldBe(1);
        await first.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenProvisionalOwnerHasNotValidated_ReturnsUnavailableWithoutOwnerIds()
    {
        var scenario = Scenario.Create();
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource<SessionRunStateResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.SessionCoordinator.OnLoadRunState = async (_, _, _) =>
        {
            loadStarted.SetResult();
            return await releaseLoad.Task.ConfigureAwait(false);
        };
        var invalidTask = scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken).AsTask();
        await loadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var whileValidating = await scenario.Coordinator.AcquireAsync(scenario.WithOperation().Request,
            scenario.Capability, TestContext.Current.CancellationToken);
        releaseLoad.SetResult(new SessionRunStateLoaded(scenario.WithOperation().State));
        var invalid = await invalidTask;
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(scenario.State));
        var successor = await scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken);

        _ = whileValidating.ShouldBeOfType<SessionRunLeaseUnavailable>();
        _ = invalid.ShouldBeOfType<SessionRunLeaseConflict>();
        await successor.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenDifferentLanesShareSession_BothAcquireIndependently()
    {
        var firstScenario = Scenario.Create();
        var secondScenario = firstScenario.WithLane();
        firstScenario.SessionCoordinator.OnLoadRunState = (request, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(
                request.Context.ExecutionLaneId == firstScenario.State.ExecutionLaneId
                    ? firstScenario.State
                    : secondScenario.State));

        var first = (SessionRunLeaseAcquired) await firstScenario.Coordinator.AcquireAsync(
            firstScenario.Request, firstScenario.Capability, TestContext.Current.CancellationToken);
        var second = await firstScenario.Coordinator.AcquireAsync(secondScenario.Request,
            secondScenario.Capability, TestContext.Current.CancellationToken);

        _ = second.ShouldBeOfType<SessionRunLeaseAcquired>();
        await first.Lease.DisposeAsync();
        await ((SessionRunLeaseAcquired) second).Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenDifferentTenantsShareCoordinates_BothAcquireIndependently()
    {
        var firstScenario = Scenario.Create();
        var secondScenario = firstScenario.WithTenant();
        firstScenario.SessionCoordinator.OnLoadRunState = (request, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(
                request.Context.Identity.TenantId == firstScenario.State.Identity.TenantId
                    ? firstScenario.State
                    : secondScenario.State));

        var first = (SessionRunLeaseAcquired) await firstScenario.Coordinator.AcquireAsync(
            firstScenario.Request, firstScenario.Capability, TestContext.Current.CancellationToken);
        var second = await firstScenario.Coordinator.AcquireAsync(secondScenario.Request,
            secondScenario.Capability, TestContext.Current.CancellationToken);

        var acquired = second.ShouldBeOfType<SessionRunLeaseAcquired>();
        acquired.Lease.TenantId.ShouldBe(secondScenario.Request.Context.Identity.TenantId);
        await first.Lease.DisposeAsync();
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenCanonicalRevisionDiffers_ReleasesProvisionalSlot()
    {
        var scenario = Scenario.Create();
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(
                scenario.State with { }));
        var staleRequest = new SessionRunLeaseRequest(scenario.Request.Context, new OperationStateRevision(2));

        var conflict = await scenario.Coordinator.AcquireAsync(staleRequest, scenario.Capability,
            TestContext.Current.CancellationToken);
        var acquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        conflict.ShouldBeOfType<SessionRunLeaseConflict>().Kind
            .ShouldBe(SessionRunLeaseConflictKind.OperationStateRevision);
        await acquired.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenRetainedProfileDiffers_ReturnsTypedConflictAndReleasesSlot()
    {
        var scenario = Scenario.Create();
        var selected = new SessionProfileSnapshot(
            new SessionProfileReference(new SessionProfileKey("other"), new SessionProfileVersion(2)),
            scenario.Profile.CoordinatorKey, scenario.Profile.RunCoordinatorKey,
            scenario.Profile.DefaultStoreKey, scenario.Profile.RequiredStoreCapabilities,
            scenario.Profile.RequiresDurableStore, scenario.Profile.RequiresDistributedFencing,
            scenario.Profile.RetentionProfile, scenario.Profile.BusyBehavior,
            scenario.Profile.MaximumAppendEntries, scenario.Profile.MaximumPageSize,
            scenario.Profile.VerifySnapshotHashes, scenario.Profile.DeleteOnDispose,
            scenario.Profile.ConfigurationFingerprint);
        var session = new SessionExecutionCapability(selected, scenario.SessionCoordinator,
            scenario.Coordinator);

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, session,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunLeaseConflict>().Kind
            .ShouldBe(SessionRunLeaseConflictKind.SessionProfile);
        var acquired = await scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken);
        await acquired.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenLoadedOperationDiffers_ReturnsTypedConflictAndReleasesSlot()
    {
        var scenario = Scenario.Create();
        var other = scenario.WithOperation();
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(other.State));

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<SessionRunLeaseConflict>().Kind
            .ShouldBe(SessionRunLeaseConflictKind.AcceptedState);
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(scenario.State));
        var acquired = await scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken);
        await acquired.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenProtectedLoadCancels_ReleasesProvisionalSlotAndPropagatesCallerToken()
    {
        var scenario = Scenario.Create();
        using var cancellation = new CancellationTokenSource();
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult<SessionRunStateResult>(
                new SessionRunStateUnavailable("A noncooperative collaborator returned after cancellation."));
        };

        var exception = await Should.ThrowAsync<OperationCanceledException>(scenario.Coordinator
            .AcquireAsync(scenario.Request, scenario.Capability, cancellation.Token).AsTask());
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(scenario.State));
        var acquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        exception.CancellationToken.ShouldBe(cancellation.Token);
        await acquired.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenWaitDeadlineAdvances_ReturnsActualBusyOwner()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var scenario = Scenario.Create(clock, SessionBusyBehavior.Wait);
        var held = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        var waiting = scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken).AsTask();

        clock.Advance(TimeSpan.FromSeconds(6));
        var busy = await waiting;

        busy.ShouldBeOfType<SessionRunBusy>().ActiveRunId.ShouldBe(scenario.Request.RunId);
        await held.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenBusyWaitTimeoutIsZero_ReturnsBusyWithoutAdvancingClock()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var scenario = Scenario.Create(clock, SessionBusyBehavior.Wait,
            busyWaitTimeout: TimeSpan.Zero);
        var held = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);

        var waiting = scenario.Coordinator.AcquireAsync(scenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken);

        waiting.IsCompleted.ShouldBeTrue();
        _ = (await waiting).ShouldBeOfType<SessionRunBusy>();
        clock.GetUtcNow().ShouldBe(DateTimeOffset.UnixEpoch);
        await held.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenCallerCancelsLocalWait_PropagatesCallerTokenAndKeepsOwner()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var scenario = Scenario.Create(clock, SessionBusyBehavior.Wait);
        var held = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var waiting = scenario.Coordinator.AcquireAsync(scenario.WithOperation().Request,
            scenario.Capability, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(waiting);
        var profile = new SessionProfileSnapshot(scenario.Profile.Reference,
            scenario.Profile.CoordinatorKey, scenario.Profile.RunCoordinatorKey,
            scenario.Profile.DefaultStoreKey, scenario.Profile.RequiredStoreCapabilities,
            scenario.Profile.RequiresDurableStore, scenario.Profile.RequiresDistributedFencing,
            scenario.Profile.RetentionProfile, SessionBusyBehavior.Reject,
            scenario.Profile.MaximumAppendEntries, scenario.Profile.MaximumPageSize,
            scenario.Profile.VerifySnapshotHashes, scenario.Profile.DeleteOnDispose,
            scenario.Profile.ConfigurationFingerprint);
        var busy = await scenario.Coordinator.AcquireAsync(scenario.Request,
            new SessionExecutionCapability(profile, scenario.SessionCoordinator, scenario.Coordinator),
            TestContext.Current.CancellationToken);

        exception.CancellationToken.ShouldBe(cancellation.Token);
        _ = busy.ShouldBeOfType<SessionRunBusy>();
        await held.Lease.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenRepeated_DoesNotReleaseAReplacementOwner()
    {
        var scenario = Scenario.Create();
        var first = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        await first.Lease.DisposeAsync();
        var replacementScenario = scenario.WithOperation();
        scenario.SessionCoordinator.OnLoadRunState = (_, _, _) =>
            ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(replacementScenario.State));
        var replacement = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            replacementScenario.Request, scenario.Capability, TestContext.Current.CancellationToken);

        await first.Lease.DisposeAsync();
        var busy = await scenario.Coordinator.AcquireAsync(replacementScenario.Request,
            scenario.Capability, TestContext.Current.CancellationToken);

        _ = busy.ShouldBeOfType<SessionRunBusy>();
        await replacement.Lease.DisposeAsync();
    }

    [Fact]
    public async Task ReleaseAsync_WhenDurableReleaseSucceeds_CallsReleaseRunWithExactEvidenceAndFreesTheLane()
    {
        var scenario = Scenario.Create();
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        var descriptor = TestFactory.Descriptor(scenario.Request.Context.ToAddress(), version: 7);
        scenario.SessionCoordinator.OnLoad = (_, _, _) =>
            ValueTask.FromResult<SessionLoadResult>(new SessionLoaded(descriptor));

        await acquired.Lease.ReleaseAsync(TestContext.Current.CancellationToken);
        var reacquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        var release = scenario.SessionCoordinator.ReleaseCalls.ShouldHaveSingleItem();
        release.Context.ShouldBe(scenario.Request.Context);
        release.ExpectedStateRevision.ShouldBe(scenario.Request.ExpectedStateRevision);
        release.ExpectedVersion.ShouldBe(descriptor.Version);
        _ = reacquired.ShouldBeOfType<SessionRunLeaseAcquired>();
        await ((SessionRunLeaseAcquired) reacquired).Lease.DisposeAsync();
    }

    [Fact]
    public async Task ReleaseAsync_WhenLoadFails_DoesNotThrowAndStillFreesTheLaneLocally()
    {
        var scenario = Scenario.Create();
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        scenario.SessionCoordinator.OnLoad = (_, _, _) =>
            ValueTask.FromResult<SessionLoadResult>(new SessionNotFound(scenario.Request.Context.ToAddress()));

        await acquired.Lease.ReleaseAsync(TestContext.Current.CancellationToken);
        var reacquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        scenario.SessionCoordinator.ReleaseCalls.ShouldBeEmpty();
        _ = reacquired.ShouldBeOfType<SessionRunLeaseAcquired>();
        await ((SessionRunLeaseAcquired) reacquired).Lease.DisposeAsync();
    }

    [Fact]
    public async Task ReleaseAsync_WhenStoreRejectsRelease_DoesNotThrowAndStillFreesTheLaneLocally()
    {
        var scenario = Scenario.Create();
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        var descriptor = TestFactory.Descriptor(scenario.Request.Context.ToAddress(), version: 3);
        scenario.SessionCoordinator.OnLoad = (_, _, _) =>
            ValueTask.FromResult<SessionLoadResult>(new SessionLoaded(descriptor));
        scenario.SessionCoordinator.OnReleaseRun = (_, _, _) =>
            ValueTask.FromResult<SessionRunReleaseResult>(
                new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.SessionVersion, "stale"));

        await acquired.Lease.ReleaseAsync(TestContext.Current.CancellationToken);
        var reacquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        _ = scenario.SessionCoordinator.ReleaseCalls.ShouldHaveSingleItem();
        _ = reacquired.ShouldBeOfType<SessionRunLeaseAcquired>();
        await ((SessionRunLeaseAcquired) reacquired).Lease.DisposeAsync();
    }

    [Fact]
    public async Task ReleaseAsync_WhenCoordinatorThrows_DoesNotThrowAndStillFreesTheLaneLocally()
    {
        var scenario = Scenario.Create();
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Capability, TestContext.Current.CancellationToken);
        scenario.SessionCoordinator.OnLoad = (_, _, _) => throw new InvalidOperationException("store unavailable");

        await acquired.Lease.ReleaseAsync(TestContext.Current.CancellationToken);
        var reacquired = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        _ = reacquired.ShouldBeOfType<SessionRunLeaseAcquired>();
        await ((SessionRunLeaseAcquired) reacquired).Lease.DisposeAsync();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AcquireAsync_WhenActivityListenerThrows_PreservesAcquisitionAndParentage(
        bool throwOnStart, bool throwOnStop)
    {
        var scenario = Scenario.Create();
        var observerFailures = 0;
        using var parent = new Activity("lease-parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleLeaseOnly,
            ActivityStarted = activity =>
            {
                if (throwOnStart && activity.OperationName == AgentKitActivityNames.SessionLeaseAcquire
                    && activity.TraceId == parent.TraceId)
                {
                    observerFailures++;
                    throw new InvalidOperationException("observer");
                }
            },
            ActivityStopped = activity =>
            {
                if (throwOnStop && activity.OperationName == AgentKitActivityNames.SessionLeaseAcquire
                    && activity.TraceId == parent.TraceId)
                {
                    observerFailures++;
                    throw new InvalidOperationException("observer");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Another subscriber can sample activities this listener did not request.
        var unrelatedTraceId = ActivityTraceId.CreateRandom();
        using var otherListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == unrelatedTraceId
                ? ActivitySamplingResult.AllData
                : ActivitySamplingResult.None,
        };
        ActivitySource.AddActivityListener(otherListener);
        using (var unrelatedParent = new Activity("unrelated-parent")
            .SetParentId(unrelatedTraceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None)
            .Start())
        {
            using var unrelated = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.SessionEntryCodec);
            _ = unrelated.ShouldNotBeNull();
            using var unrelatedLease = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.SessionLeaseAcquire);
            _ = unrelatedLease.ShouldNotBeNull();
        }

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        var acquired = result.ShouldBeOfType<SessionRunLeaseAcquired>();
        Activity.Current.ShouldBeSameAs(parent);
        observerFailures.ShouldBeGreaterThan(0);
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenObserved_EmitsExactTenantLaneAndRunCorrelation()
    {
        using var parent = new Activity("observed-lease-parent").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleLeaseOnly,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionLeaseAcquire
                    && activity.TraceId == parent.TraceId)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var scenario = Scenario.Create();

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.TenantId)
            .ShouldBe(scenario.Request.Context.Identity.TenantId.ToString());
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(scenario.Request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(scenario.Request.SessionId.ToString());
        activity.GetTagItem(AgentKitTagNames.ExecutionLaneId)
            .ShouldBe(scenario.Request.ExecutionLaneId.ToString());
        activity.GetTagItem(AgentKitTagNames.OperationId).ShouldBe(scenario.Request.OperationId.ToString());
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(scenario.Request.RunId.ToString());
        activity.GetTagItem(AgentKitTagNames.TurnId).ShouldBe(
            ((InRunOperationCorrelation) scenario.Request.Context.Correlation).TurnId?.ToString());
        await result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenLoggerThrows_PreservesAcquisition()
    {
        var scenario = Scenario.Create(logger: new ThrowingLogger());

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        await result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenLogged_EmitsExactSafeOwnershipFields()
    {
        var logger = new RecordingLogger();
        var scenario = Scenario.Create(logger: logger);

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        var started = logger.Entries.Single(static entry => entry.EventId.Id == 6005).Properties;
        started["TenantId"].ShouldBe(scenario.Request.Context.Identity.TenantId);
        started["AgentId"].ShouldBe(scenario.Request.AgentId);
        started["SessionId"].ShouldBe(scenario.Request.SessionId);
        started["ExecutionLaneId"].ShouldBe(scenario.Request.ExecutionLaneId);
        started["OperationId"].ShouldBe(scenario.Request.OperationId);
        started["RunId"].ShouldBe(scenario.Request.RunId);
        started["TurnId"].ShouldBe(
            ((InRunOperationCorrelation) scenario.Request.Context.Correlation).TurnId);
        started.Keys.ShouldNotContain("Content");
        await result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenMetricCallbackThrows_PreservesAcquisition()
    {
        using var parent = new Activity("metric-lease-parent").Start();
        var scenario = Scenario.Create();
        using var listener = new MeterListener
        {
            InstrumentPublished = static (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name == AgentKitMetricNames.SessionOperationCount)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (Activity.Current?.TraceId == parent.TraceId
                    && tag.Key == AgentKitTagNames.SessionOperation
                    && Equals(tag.Value, AgentKitActivityNames.SessionLeaseAcquire))
                {
                    throw new InvalidOperationException("observer");
                }
            }
        });
        listener.Start();

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Capability,
            TestContext.Current.CancellationToken);

        await result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease.DisposeAsync();
    }

    private static ActivitySamplingResult SampleLeaseOnly(
        ref ActivityCreationOptions<ActivityContext> options) =>
        options.Name == AgentKitActivityNames.SessionLeaseAcquire
            ? ActivitySamplingResult.AllData
            : ActivitySamplingResult.None;

    private sealed record Scenario(
        DefaultSessionRunCoordinator Coordinator,
        FakeRunStateSessionCoordinator SessionCoordinator,
        SessionRunLeaseRequest Request,
        SessionProfileSnapshot Profile,
        SessionAcceptedRunState State)
    {
        internal SessionExecutionCapability Capability => new(Profile, SessionCoordinator, Coordinator);

        internal static Scenario Create(TimeProvider? clock = null,
            SessionBusyBehavior busyBehavior = SessionBusyBehavior.Reject,
            ILogger<DefaultSessionRunCoordinator>? logger = null,
            TimeSpan? busyWaitTimeout = null)
        {
            var profile = TestFactory.Profile() with { };
            if (busyBehavior != profile.BusyBehavior)
            {
                profile = new SessionProfileSnapshot(profile.Reference, profile.CoordinatorKey,
                    profile.RunCoordinatorKey, profile.DefaultStoreKey, profile.RequiredStoreCapabilities,
                    profile.RequiresDurableStore, profile.RequiresDistributedFencing, profile.RetentionProfile,
                    busyBehavior, profile.MaximumAppendEntries, profile.MaximumPageSize,
                    profile.VerifySnapshotHashes, profile.DeleteOnDispose, profile.ConfigurationFingerprint);
            }
            var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
            var lane = new ExecutionLaneId(Guid.NewGuid());
            var operation = new OperationId(Guid.NewGuid());
            var correlation = new InRunOperationCorrelation(operation, new RunId(Guid.NewGuid()),
                new TurnId(Guid.NewGuid()));
            var identity = TestFactory.Identity();
            var context = new SessionOperationContext(address.AgentId, address.SessionId, lane, correlation,
                identity, TestFactory.Authorization(address.AgentId, address.SessionId, correlation, identity));
            var request = new SessionRunLeaseRequest(context, new OperationStateRevision(1));
            var state = CreateState(request, profile.Reference);
            var sessionCoordinator = new FakeRunStateSessionCoordinator
            {
                OnLoadRunState = (_, _, _) => ValueTask.FromResult<SessionRunStateResult>(
                    new SessionRunStateLoaded(state)),
            };
            var coordinator = new DefaultSessionRunCoordinator(
                new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)),
                clock ?? TimeProvider.System, Options.Create(new AgentSessionOptions
                {
                    BusyWaitTimeout = busyWaitTimeout ?? TimeSpan.FromSeconds(5),
                }), logger);
            return new Scenario(coordinator, sessionCoordinator, request, profile, state);
        }

        internal Scenario WithOperation()
        {
            var address = Request.Context.ToAddress();
            var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()),
                new RunId(Guid.NewGuid()), new TurnId(Guid.NewGuid()));
            var context = new SessionOperationContext(address.AgentId, address.SessionId,
                Request.ExecutionLaneId, correlation, Request.Context.Identity,
                TestFactory.Authorization(address.AgentId, address.SessionId, correlation,
                    Request.Context.Identity));
            var request = new SessionRunLeaseRequest(context, Request.ExpectedStateRevision);
            return this with { Request = request, State = CreateState(request, Profile.Reference) };
        }

        internal Scenario WithLane()
        {
            var context = new SessionOperationContext(Request.AgentId, Request.SessionId,
                new ExecutionLaneId(Guid.NewGuid()), Request.Context.Correlation, Request.Context.Identity,
                Request.Context.Authorization);
            var request = new SessionRunLeaseRequest(context, Request.ExpectedStateRevision);
            return this with { Request = request, State = CreateState(request, Profile.Reference) };
        }

        internal Scenario WithTenant()
        {
            var identity = TestFactory.Identity("tenant-2");
            var correlation = Request.Context.Correlation;
            var context = new SessionOperationContext(Request.AgentId, Request.SessionId,
                Request.ExecutionLaneId, correlation, identity,
                TestFactory.Authorization(Request.AgentId, Request.SessionId, correlation, identity));
            var request = new SessionRunLeaseRequest(context, Request.ExpectedStateRevision);
            return this with { Request = request, State = CreateState(request, Profile.Reference) };
        }

        internal Scenario WithPrincipal(string principal)
        {
            var identity = TestFactory.Identity(Request.Context.Identity.TenantId.Value, principal);
            var correlation = Request.Context.Correlation;
            var context = new SessionOperationContext(Request.AgentId, Request.SessionId,
                Request.ExecutionLaneId, correlation, identity,
                TestFactory.Authorization(Request.AgentId, Request.SessionId, correlation, identity));
            var request = new SessionRunLeaseRequest(context, Request.ExpectedStateRevision);
            return this with { Request = request, State = CreateState(request, Profile.Reference) };
        }

        private static SessionAcceptedRunState CreateState(SessionRunLeaseRequest request,
            SessionProfileReference profile)
        {
            var branchId = new BranchId(Guid.NewGuid());
            var admissionId = new AdmissionId(Guid.NewGuid());
            return new SessionAcceptedRunState(
                request.Context.ToAddress(), request.ExecutionLaneId, new SessionLaneRevision(2),
                (InRunOperationCorrelation) request.Context.Correlation, request.ExpectedStateRevision,
                request.Context.Identity, request.Context.Authorization, profile,
                new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1),
                    new ContentHash("sha256:configuration")),
                new SessionBranchCursor(branchId, new SessionEntryId(Guid.NewGuid())),
                new SessionBranchCursor(branchId, new SessionEntryId(Guid.NewGuid())),
                new SessionSequence(1), admissionId, [admissionId],
                [new SessionEntryId(Guid.NewGuid())], [new MessageId(Guid.NewGuid())],
                ((InRunOperationCorrelation) request.Context.Correlation).TurnId!.Value,
                DateTimeOffset.UnixEpoch);
        }
    }

    private sealed class ThrowingLogger: ILogger<DefaultSessionRunCoordinator>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("observer");
    }

    private sealed class RecordingLogger: ILogger<DefaultSessionRunCoordinator>
    {
        internal List<(EventId EventId, IReadOnlyDictionary<string, object?> Properties)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IReadOnlyList<KeyValuePair<string, object?>> values
                ? values.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Entries.Add((eventId, properties));
        }
    }

    private sealed class ThrowOnceLeaseIdGenerator: IIdentifierGenerator<SessionLeaseId>
    {
        private int _calls;

        public SessionLeaseId Create() => Interlocked.Increment(ref _calls) == 1
            ? throw new InvalidOperationException("identity source failed")
            : new SessionLeaseId(Guid.NewGuid());
    }

    private sealed class NullSessionOptions: IOptions<AgentSessionOptions>
    {
        public AgentSessionOptions Value => null!;
    }
}
