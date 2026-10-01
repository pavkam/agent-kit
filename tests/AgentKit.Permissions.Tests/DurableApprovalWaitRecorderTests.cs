// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Options;

/// <summary>Verifies the approval-wait recorder journals a deferred approval and never changes what its caller does next.</summary>
public sealed class DurableApprovalWaitRecorderTests
{
    private static readonly DurabilityProfileKey Profile = new("permissions-durability");
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies the recorder requires the options, clock, and registry it cannot run without.</summary>
    [Fact]
    public void Constructor_WhenARequiredDependencyIsNull_ThrowsForThatArgument()
    {
        var options = Options.Create(new AgentPermissionOptions());
        var registry = new DurableBoundaryRegistry();

        Should.Throw<ArgumentNullException>(() => new DurableApprovalWaitRecorder(null!, TimeProvider.System, registry))
            .ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new DurableApprovalWaitRecorder(options, null!, registry))
            .ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new DurableApprovalWaitRecorder(options, TimeProvider.System, null!))
            .ParamName.ShouldBe("durableInvocations");
    }

    /// <summary>Verifies the request and approval are validated before anything is journaled.</summary>
    [Fact]
    public async Task RecordAsync_WhenAnArgumentIsNull_ThrowsForThatArgument()
    {
        var recorder = Recorder(new RecordingBoundaryCoordinator(new DurableBoundaryRegistry()), registry: null);

        (await Should.ThrowAsync<ArgumentNullException>(
            () => recorder.RecordAsync(null!, Approval(Captured()), TestContext.Current.CancellationToken).AsTask()))
            .ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(
            () => recorder.RecordAsync(Captured(), null!, TestContext.Current.CancellationToken).AsTask()))
            .ParamName.ShouldBe("approval");
    }

    /// <summary>Verifies an enabled wait is journaled as a not-performed wait naming the pending approval.</summary>
    [Fact]
    public async Task RecordAsync_WhenTheProfileEnablesApprovalWaits_JournalsAWaitNamingThePendingApproval()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry);
        var request = Captured();
        var approval = Approval(request);
        var recorder = Recorder(coordinator, registry, PermissionsDurableOperations.ApprovalWait);

        await recorder.RecordAsync(request, approval, TestContext.Current.CancellationToken);

        var declared = coordinator.Executions.ShouldHaveSingleItem();
        declared.Name.ShouldBe(PermissionsDurableOperations.ApprovalWait);
        declared.Effect.ShouldBe(SecurityEffect.Observe);
        var manifest = DurableBoundaryPayload.Decode<DurableApprovalWaitManifest>(declared.Input);
        manifest.SecurityRequestId.ShouldBe(request.Id.Value);
        manifest.ApprovalRequestId.ShouldBe(approval.Id.Value);
        var wait = coordinator.Writers.ShouldHaveSingleItem().Waits.ShouldHaveSingleItem();
        wait.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        wait.ExternalReference.ShouldNotBeNull().Handle.ShouldBe(approval.Id.Value.ToString());
        wait.NotBefore.ShouldBeNull();
    }

    /// <summary>Verifies the recorded manifest never carries the approval prompt or the requested resources.</summary>
    [Fact]
    public async Task RecordAsync_WhenJournaled_KeepsThePromptAndResourcesOutOfTheDurablePayload()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry);
        var request = Captured();
        var recorder = Recorder(coordinator, registry, PermissionsDurableOperations.ApprovalWait);

        await recorder.RecordAsync(request, Approval(request), TestContext.Current.CancellationToken);

        var payload = System.Text.Encoding.UTF8.GetString(coordinator.Executions.Single().Input.Data.AsSpan());
        payload.ShouldNotContain("Approve a bounded test operation.");
        payload.ShouldNotContain("/workspace/file.txt");
    }

    /// <summary>Verifies no profile means no durable record and no coordinator call.</summary>
    [Fact]
    public async Task RecordAsync_WhenNoProfileIsConfigured_RecordsNothing()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry);
        var request = Captured();
        var recorder = new DurableApprovalWaitRecorder(
            Options.Create(new AgentPermissionOptions()),
            new FakeTimeProvider(Now),
            registry,
            coordinator,
            new FixedDurabilityProfileCatalog(Profile, PermissionsDurableOperations.ApprovalWait));

        await recorder.RecordAsync(request, Approval(request), TestContext.Current.CancellationToken);

        coordinator.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies a profile that does not list the boundary leaves the decision undurable without an error.</summary>
    [Fact]
    public async Task RecordAsync_WhenTheProfileDoesNotEnableApprovalWaits_RecordsNothing()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry);
        var request = Captured();
        var logger = new RecordingLogger<DurableApprovalWaitRecorder>();
        var recorder = Recorder(coordinator, registry, logger: logger);

        await recorder.RecordAsync(request, Approval(request), TestContext.Current.CancellationToken);

        coordinator.Executions.ShouldBeEmpty();
        logger.Snapshot().ShouldBeEmpty();
    }

    /// <summary>Verifies a selected profile the composition cannot honor is logged, never thrown into the caller.</summary>
    [Fact]
    public async Task RecordAsync_WhenTheSelectedProfileIsNotRegistered_LogsAndRecordsNothing()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry);
        var request = Captured();
        var logger = new RecordingLogger<DurableApprovalWaitRecorder>();
        var recorder = new DurableApprovalWaitRecorder(
            Options.Create(new AgentPermissionOptions { DurabilityProfile = Profile }),
            new FakeTimeProvider(Now),
            registry,
            coordinator,
            new FixedDurabilityProfileCatalog(new DurabilityProfileKey("another-profile")),
            logger);

        await recorder.RecordAsync(request, Approval(request), TestContext.Current.CancellationToken);

        coordinator.Executions.ShouldBeEmpty();
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(5029);
    }

    /// <summary>Verifies a request with no captured authorization has no address to journal under and is logged.</summary>

    /// <summary>Verifies a durability runtime that fails is logged and isolated, because a wait is evidence and not authority.</summary>
    [Fact]
    public async Task RecordAsync_WhenTheCoordinatorFails_LogsTheFailureWithoutThrowing()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry) { Failure = new InvalidOperationException("journal offline") };
        var request = Captured();
        var logger = new RecordingLogger<DurableApprovalWaitRecorder>();
        var recorder = Recorder(coordinator, registry, PermissionsDurableOperations.ApprovalWait, logger: logger);

        await recorder.RecordAsync(request, Approval(request), TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(5029);
        entry.Message.ShouldNotContain("journal offline");
    }

    /// <summary>Verifies cancellation is never swallowed into the durability-gap log.</summary>
    [Fact]
    public async Task RecordAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        var registry = new DurableBoundaryRegistry();
        var coordinator = new RecordingBoundaryCoordinator(registry);
        var request = Captured();
        var recorder = Recorder(coordinator, registry, PermissionsDurableOperations.ApprovalWait);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => recorder.RecordAsync(request, Approval(request), cancelled.Token).AsTask());

        coordinator.Executions.ShouldBeEmpty();
    }

    private static DurableApprovalWaitRecorder Recorder(
        RecordingBoundaryCoordinator coordinator,
        DurableBoundaryRegistry? registry,
        DurableOperationName? enabled = null,
        RecordingLogger<DurableApprovalWaitRecorder>? logger = null) => new(
        Options.Create(new AgentPermissionOptions { DurabilityProfile = Profile }),
        new FakeTimeProvider(Now),
        registry ?? new DurableBoundaryRegistry(),
        coordinator,
        enabled is { } name
            ? new FixedDurabilityProfileCatalog(Profile, name)
            : new FixedDurabilityProfileCatalog(Profile),
        logger);

    private static SecurityRequest Captured()
    {
        var baseline = SecurityAuthorityTestData.CreateRequest(Now);
        var authorization = TestSecurityEvidence.Authorization(
            baseline.Scope.AgentId,
            baseline.Scope.SessionId,
            baseline.Scope.Correlation,
            baseline.Identity);
        return new SecurityRequest(
            baseline.Id,
            baseline.Scope,
            baseline.ToolCallId,
            baseline.Identity,
            authorization,
            baseline.Audience,
            baseline.Kind,
            baseline.Effect,
            baseline.Resources,
            baseline.InputFingerprint,
            baseline.Deadline);
    }

    private static ApprovalRequest Approval(SecurityRequest request) => new(
        new ApprovalRequestId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
        new ApprovalScopeBinding(
            request, new SecurityPolicyVersion(1), new SecurityRevocationVersion(1), Now, Now.AddMinutes(5), 1),
        "Approve a bounded test operation.",
        Now);
}
