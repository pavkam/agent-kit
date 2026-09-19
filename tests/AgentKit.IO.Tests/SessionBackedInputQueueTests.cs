// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies SessionBackedInputQueue behavior and contracts.</summary>
public sealed class SessionBackedInputQueueTests
{
    [Fact]
    public async Task AppendAsync_WhenSessionAndLaneAreResolved_AdmitsWithDiscoveredLaneEvidence()
    {
        var coordinator = new FakeSessionCoordinator
        {
            OnLoad = _ => new SessionLoaded(Descriptor(new SessionVersion(9))),
            OnLoadLaneState = _ => new SessionLaneStateLoaded(
                new SessionLaneState(InputCoordinationTestData.Lane, new SessionLaneRevision(3), Cursor(), null)),
            OnAdmitInput = _ => InputCoordinationTestData.Accepted(
                new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid())),
        };
        var queue = CreateQueue(coordinator);
        var request = BeforeRunAdmissionRequest();
        var admissionId = new AdmissionId(Guid.NewGuid());

        var result = await queue.AppendAsync(
            request, admissionId, request.Input, Preprocessing(request.Input), DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AcceptedInput>();
        var admission = coordinator.ReceivedAdmission.ShouldNotBeNull();
        admission.ExpectedVersion.ShouldBe(new SessionVersion(9));
        admission.ExpectedLaneRevision.ShouldBe(new SessionLaneRevision(3));
        admission.BranchCursor.ShouldBe(Cursor());
        admission.AdmissionId.ShouldBe(admissionId);
    }

    [Fact]
    public async Task AppendAsync_WhenSessionIsNotFound_ReturnsRejectedInputWithoutAdmitting()
    {
        var coordinator = new FakeSessionCoordinator
        {
            OnLoad = _ => new SessionNotFound(new SessionAddress(InputCoordinationTestData.Agent, InputCoordinationTestData.Session)),
        };
        var queue = CreateQueue(coordinator);
        var request = BeforeRunAdmissionRequest();

        var result = await queue.AppendAsync(
            request, new AdmissionId(Guid.NewGuid()), request.Input, Preprocessing(request.Input),
            DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<RejectedInput>();
        rejected.Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
        coordinator.ReceivedAdmission.ShouldBeNull();
    }

    [Fact]
    public async Task AppendAsync_WhenLaneIsNotProvisioned_ReturnsRejectedInputWithoutAdmitting()
    {
        var coordinator = new FakeSessionCoordinator
        {
            OnLoad = _ => new SessionLoaded(Descriptor(new SessionVersion(1))),
            OnLoadLaneState = _ => new SessionLaneStateNotProvisioned("not provisioned"),
        };
        var queue = CreateQueue(coordinator);
        var request = BeforeRunAdmissionRequest();

        var result = await queue.AppendAsync(
            request, new AdmissionId(Guid.NewGuid()), request.Input, Preprocessing(request.Input),
            DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<RejectedInput>();
        rejected.Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
        coordinator.ReceivedAdmission.ShouldBeNull();
    }

    [Fact]
    public async Task PromoteAsync_WhenEligibleInputExceedsCutoffAndLimit_SelectsOnlyEligibleWithinBound()
    {
        var early = Admitted(new SessionSequence(1));
        var late = Admitted(new SessionSequence(2));
        var afterCutoff = Admitted(new SessionSequence(11));
        var coordinator = new FakeSessionCoordinator
        {
            OnLoadPendingInputs = _ => new SessionPendingInputsLoaded(
                InputCoordinationTestData.Agent, InputCoordinationTestData.Session, InputCoordinationTestData.Lane,
                [early, late, afterCutoff], new SessionLaneRevision(2), Cursor()),
            OnPromoteInput = _ => new SessionInputPromoted(
                [WithPromoted(early), WithPromoted(late)], Cursor(), new SessionVersion(5), new OperationStateRevision(3)),
        };
        var queue = CreateQueue(coordinator);
        var request = InputCoordinationTestData.PromotionRequest();

        var result = await queue.PromoteAsync(request, TestContext.Current.CancellationToken);

        var promoted = result.ShouldBeOfType<InputPromoted>();
        promoted.Promoted.Length.ShouldBe(2);
        var promotion = coordinator.ReceivedPromotion.ShouldNotBeNull();
        promotion.SelectedAdmissionIds.ShouldBe([early.AdmissionId, late.AdmissionId]);
        promotion.ExpectedLaneRevision.ShouldBe(new SessionLaneRevision(2));
    }

    [Fact]
    public async Task PromoteAsync_WhenNothingIsEligible_ReturnsRejectedWithNoEligibleInputKind()
    {
        var coordinator = new FakeSessionCoordinator
        {
            OnLoadPendingInputs = _ => new SessionPendingInputsLoaded(
                InputCoordinationTestData.Agent, InputCoordinationTestData.Session, InputCoordinationTestData.Lane,
                [], new SessionLaneRevision(1), Cursor()),
        };
        var queue = CreateQueue(coordinator);

        var result = await queue.PromoteAsync(InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<InputPromotionRejected>();
        rejected.Rejection.Kind.ShouldBe(InputRejectionKind.NoEligibleInput);
        coordinator.ReceivedPromotion.ShouldBeNull();
    }

    [Fact]
    public async Task PromoteAsync_WhenPendingDiscoveryIsUnavailable_ReturnsRejectedWithoutPromoting()
    {
        var coordinator = new FakeSessionCoordinator
        {
            OnLoadPendingInputs = _ => new SessionPendingInputsUnavailable("unavailable"),
        };
        var queue = CreateQueue(coordinator);

        var result = await queue.PromoteAsync(InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<InputPromotionRejected>();
        rejected.Rejection.Kind.ShouldBe(InputRejectionKind.AddressNotFound);
        coordinator.ReceivedPromotion.ShouldBeNull();
    }

    [Fact]
    public async Task PromoteAsync_WhenStoreRejects_PropagatesSafeReason()
    {
        var eligible = Admitted(new SessionSequence(1));
        var coordinator = new FakeSessionCoordinator
        {
            OnLoadPendingInputs = _ => new SessionPendingInputsLoaded(
                InputCoordinationTestData.Agent, InputCoordinationTestData.Session, InputCoordinationTestData.Lane,
                [eligible], new SessionLaneRevision(2), Cursor()),
            OnPromoteInput = _ => new SessionInputPromotionRejected("stale evidence"),
        };
        var queue = CreateQueue(coordinator);

        var result = await queue.PromoteAsync(InputCoordinationTestData.PromotionRequest(), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<InputPromotionRejected>();
        rejected.Rejection.SafeReason.ShouldBe("stale evidence");
    }

    private static InputAdmissionRequest BeforeRunAdmissionRequest()
    {
        var correlation = new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("profile"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()), new SecurityPolicyVersion(1), new ContentHash("safe")),
            new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(0), new ConfigurationVersion(1),
            new SecurityAuthorizationScope(InputCoordinationTestData.Agent, InputCoordinationTestData.Session, correlation),
            InputCoordinationTestData.Identity());
        return new InputAdmissionRequest(
            InputCoordinationTestData.Agent, InputCoordinationTestData.Session, InputCoordinationTestData.Lane,
            InputCoordinationTestData.Identity(), correlation, authorization, InputCoordinationTestData.Payload());
    }

    private static SessionBackedInputQueue CreateQueue(FakeSessionCoordinator coordinator)
    {
        var capability = new SessionExecutionCapability(
            TestSecurityEvidence.SessionProfile(), coordinator, new UnsupportedSessionRunCoordinator());
        return new SessionBackedInputQueue(
            coordinator, capability, new GuidSessionEntryIdGenerator(), new GuidMessageIdGenerator(),
            TimeProvider.System);
    }

    private static SessionDescriptor Descriptor(SessionVersion version) => new(
        new SessionAddress(InputCoordinationTestData.Agent, InputCoordinationTestData.Session), null,
        new TenantId("tenant"), new PrincipalId("principal"), new SessionStoreKey("test-store"),
        InputCoordinationTestData.Branch, version, SessionLifecycleState.Active, DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch, new SchemaVersion("1"), ExtensionData.Empty);

    private static SessionBranchCursor Cursor() => new(InputCoordinationTestData.Branch, InputCoordinationTestData.Entry);

    private static InputPreprocessingManifest Preprocessing(AgentInput input)
    {
        var fingerprint = InputPayloadFingerprint.Create(input);
        return new InputPreprocessingManifest(new ConfigurationVersion(1), fingerprint, fingerprint);
    }

    private static AdmittedInput Admitted(SessionSequence sequence)
    {
        var payload = InputCoordinationTestData.Payload($"pending-{sequence.Value}");
        return new AdmittedInput(
            new AdmissionId(Guid.NewGuid()), InputCoordinationTestData.Agent, InputCoordinationTestData.Session,
            InputCoordinationTestData.Lane, InputCoordinationTestData.Identity(), sequence, payload, payload,
            new InputPreprocessingManifest(new ConfigurationVersion(1), InputPayloadFingerprint.Create(payload), InputPayloadFingerprint.Create(payload)),
            DateTimeOffset.UnixEpoch);
    }

    private static AdmittedInput WithPromoted(AdmittedInput input) => new(
        input.AdmissionId, input.AgentId, input.SessionId, input.ExecutionLaneId, input.Identity,
        input.AdmittedSequence, input.OriginalPayload, input.EffectivePayload, input.Preprocessing, input.AdmittedAt,
        new SessionSequence(input.AdmittedSequence.Value + 100));
}
