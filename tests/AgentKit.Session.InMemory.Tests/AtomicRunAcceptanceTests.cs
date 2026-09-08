// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

public sealed class AtomicRunAcceptanceTests
{
    [Fact]
    public async Task AdmitInputAsync_WhenAdmissionIdentityCollides_DoesNotAdvanceAnySessionState()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var context = TestFactory.LaneContext(
            descriptor.Address, new ExecutionLaneId(Guid.NewGuid()),
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), identity);
        var profile = new SessionProfileReference(new SessionProfileKey("default"), new SessionProfileVersion(1));
        var configuration = new RunConfigurationReference(
            new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            new SessionExecutionLaneProvisionRequest(
                context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
                new SessionEntryId(Guid.NewGuid()), profile, configuration, DateTimeOffset.UnixEpoch,
                new IdempotencyKey("provision-collision")),
            TestContext.Current.CancellationToken);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var first = Admission(context, admissionId, new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "first");
        var firstAccepted = (AcceptedInput) await store.AdmitInputAsync(first, TestContext.Current.CancellationToken);
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(descriptor.ActiveBranchId, first.EntryId);
        var collision = Admission(context, admissionId, new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()),
            currentVersion, currentRevision, currentCursor, "collision");
        var subsequent = Admission(context, new AdmissionId(Guid.NewGuid()), new InputId(Guid.NewGuid()),
            new SessionEntryId(Guid.NewGuid()), currentVersion, currentRevision, currentCursor, "subsequent");

        var rejected = await store.AdmitInputAsync(collision, TestContext.Current.CancellationToken);
        var accepted = await store.AdmitInputAsync(subsequent, TestContext.Current.CancellationToken);

        _ = rejected.ShouldBeOfType<InputConflict>();
        firstAccepted.Receipt.AdmittedSequence.Value.ShouldBe(2);
        accepted.ShouldBeOfType<AcceptedInput>().Receipt.AdmittedSequence.Value.ShouldBe(3);
    }

    [Fact]
    public async Task AcceptRunAsync_WhenLaneAndAdmissionAreCurrent_CommitsRecoverableStateAndReplaysBeforeVersionChecks()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var operationId = new OperationId(Guid.NewGuid());
        var beforeCorrelation = new BeforeRunOperationCorrelation(operationId, null);
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var context = TestFactory.LaneContext(descriptor.Address, laneId, beforeCorrelation, identity);
        var profile = new SessionProfileReference(new SessionProfileKey("default"), new SessionProfileVersion(1));
        var configuration = new RunConfigurationReference(
            new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provision = new SessionExecutionLaneProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            new SessionEntryId(Guid.NewGuid()), profile, configuration, DateTimeOffset.UnixEpoch,
            new IdempotencyKey("provision"));

        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            provision, TestContext.Current.CancellationToken);
        var input = new AgentInput(
            new InputId(Guid.NewGuid()), InputDelivery.FollowUp,
            [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var admission = new SessionInputAdmissionRequest(
            context, admissionId, new SessionEntryId(Guid.NewGuid()), input, input,
            new InputPreprocessingManifest(
                new ConfigurationVersion(1), new InputFingerprint("sha256:original"),
                new InputFingerprint("sha256:effective")),
            DateTimeOffset.UnixEpoch.AddSeconds(1), provisioned.SessionVersion,
            provisioned.LaneRevision, provisioned.BranchCursor, new IdempotencyKey("admit"), 8);
        var admitted = (AcceptedInput) await store.AdmitInputAsync(admission, TestContext.Current.CancellationToken);
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var inRunCorrelation = new InRunOperationCorrelation(operationId, runId, turnId);
        var inRunAuthorization = TestFactory.Authorization(
            descriptor.Address.AgentId, descriptor.Address.SessionId, inRunCorrelation, identity);
        var start = new SessionRunStartRequest(
            context, admissionId, [admissionId], admitted.Receipt.AdmittedSequence,
            new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionVersion(provisioned.SessionVersion.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), null, runId, turnId,
            new SessionEntryId(Guid.NewGuid()), [new SessionEntryId(Guid.NewGuid())],
            [new MessageId(Guid.NewGuid())], new SessionEntryId(Guid.NewGuid()),
            new OperationStateRevision(1), profile, configuration, inRunAuthorization,
            DateTimeOffset.UnixEpoch.AddSeconds(2), new IdempotencyKey("start"));
        var colliding = new SessionRunStartRequest(
            context, admissionId, [admissionId], admitted.Receipt.AdmittedSequence,
            start.ExpectedLaneRevision, start.ExpectedVersion, start.BranchCursor, null, runId, turnId,
            provision.EntryId, start.EntryIds, start.MessageIds, start.AcceptedEntryId,
            start.OperationStateRevision, profile, configuration, inRunAuthorization,
            start.AcceptedAt, new IdempotencyKey("colliding-start"));

        var collision = await store.AcceptRunAsync(colliding, TestContext.Current.CancellationToken);
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);
        var replay = (SessionRunAccepted) await store.AcceptRunAsync(start, TestContext.Current.CancellationToken);
        var loadContext = TestFactory.LaneContext(descriptor.Address, laneId, inRunCorrelation, identity);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            new SessionRunStateRequest(loadContext), TestContext.Current.CancellationToken);

        _ = collision.ShouldBeOfType<SessionRunStartConflict>();
        accepted.Existing.ShouldBeFalse();
        replay.Existing.ShouldBeTrue();
        replay.State.ShouldBe(accepted.State);
        loaded.State.ShouldBe(accepted.State);
        accepted.State.State.ShouldBe(DurableOperationState.Accepted);
        accepted.State.InitiatingAdmissionId.ShouldBe(admissionId);
        accepted.State.PromotedAdmissionIds.ShouldBe([admissionId]);
        accepted.State.CommittedCursor.LastEntryId.ShouldBe(start.AcceptedEntryId);
        var page = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(loadContext, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);
        page.Entries.OfType<InputPromotedSessionEntry>().Single().InitiatingAdmissionId.ShouldBe(admissionId);
        page.Entries[^1].ShouldBeOfType<OperationAcceptedSessionEntry>()
            .State.InitiatingAdmissionId.ShouldBe(admissionId);
    }

    [Fact]
    public async Task AcceptRunAsync_WhenInitiatingCorrelationDiffers_RejectsWithoutMutationAndRetainsTriggerForRecovery()
    {
        var store = TestFactory.CreateStore();
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var identity = TestFactory.Identity();
        var storedOperationId = new OperationId(Guid.NewGuid());
        var laneId = new ExecutionLaneId(Guid.NewGuid());
        var storedContext = TestFactory.LaneContext(
            descriptor.Address, laneId,
            new BeforeRunOperationCorrelation(storedOperationId, null), identity);
        var profile = new SessionProfileReference(
            new SessionProfileKey("default"), new SessionProfileVersion(1));
        var configuration = new RunConfigurationReference(
            new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            new SessionExecutionLaneProvisionRequest(
                storedContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
                new SessionEntryId(Guid.NewGuid()), profile, configuration, DateTimeOffset.UnixEpoch,
                new IdempotencyKey("provision-correlation")),
            TestContext.Current.CancellationToken);
        var admissionId = new AdmissionId(Guid.NewGuid());
        var admission = Admission(
            storedContext, admissionId, new InputId(Guid.NewGuid()), new SessionEntryId(Guid.NewGuid()),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor,
            "admit-correlation");
        var admitted = (AcceptedInput) await store.AdmitInputAsync(
            admission, TestContext.Current.CancellationToken);
        var expectedVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var expectedLaneRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var expectedCursor = new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId);
        var substitutedContext = TestFactory.LaneContext(
            descriptor.Address, laneId,
            new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), identity);
        var rejectedStart = Start(
            substitutedContext, admissionId, admitted.Receipt.AdmittedSequence,
            expectedLaneRevision, expectedVersion, expectedCursor, profile, configuration, "reject-correlation");

        var rejected = await store.AcceptRunAsync(rejectedStart, TestContext.Current.CancellationToken);
        var afterRejection = (SessionLoaded) await store.LoadAsync(
            storedContext, TestContext.Current.CancellationToken);
        var rejectedPage = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(
                storedContext, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<SessionRunStartConflict>().Kind
            .ShouldBe(SessionRunStartConflictKind.AdmissionCorrelation);
        afterRejection.Descriptor.Version.ShouldBe(expectedVersion);
        rejectedPage.Entries.Length.ShouldBe(2);
        rejectedPage.Entries.ShouldNotContain(static entry => entry is InputPromotedSessionEntry);
        rejectedPage.Entries.ShouldNotContain(static entry => entry is OperationAcceptedSessionEntry);

        var acceptedStart = Start(
            storedContext, admissionId, admitted.Receipt.AdmittedSequence,
            expectedLaneRevision, expectedVersion, expectedCursor, profile, configuration, "accept-correlation");
        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            acceptedStart, TestContext.Current.CancellationToken);
        var inRunContext = TestFactory.LaneContext(
            descriptor.Address, laneId,
            new InRunOperationCorrelation(storedOperationId, acceptedStart.RunId, acceptedStart.InitialTurnId),
            identity);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            new SessionRunStateRequest(inRunContext), TestContext.Current.CancellationToken);
        var acceptedPage = (SessionPage) await store.ReadAsync(
            new SessionReadRequest(inRunContext, descriptor.ActiveBranchId, new SessionSequence(0), 10),
            TestContext.Current.CancellationToken);

        loaded.State.ShouldBe(accepted.State);
        loaded.State.Correlation.OperationId.ShouldBe(storedOperationId);
        loaded.State.InitiatingAdmissionId.ShouldBe(admissionId);
        acceptedPage.Entries.OfType<InputPromotedSessionEntry>().Single()
            .InitiatingAdmissionId.ShouldBe(admissionId);
        acceptedPage.Entries[^1].ShouldBeOfType<OperationAcceptedSessionEntry>()
            .State.InitiatingAdmissionId.ShouldBe(admissionId);
    }

    private static SessionRunStartRequest Start(
        SessionOperationContext context,
        AdmissionId admissionId,
        SessionSequence cutoff,
        SessionLaneRevision expectedLaneRevision,
        SessionVersion expectedVersion,
        SessionBranchCursor branchCursor,
        SessionProfileReference profile,
        RunConfigurationReference configuration,
        string idempotencyKey)
    {
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var inRunCorrelation = new InRunOperationCorrelation(
            context.Correlation.OperationId, runId, turnId);
        return new SessionRunStartRequest(
            context, admissionId, [admissionId], cutoff, expectedLaneRevision, expectedVersion,
            branchCursor, null, runId, turnId, new SessionEntryId(Guid.NewGuid()),
            [new SessionEntryId(Guid.NewGuid())], [new MessageId(Guid.NewGuid())],
            new SessionEntryId(Guid.NewGuid()), new OperationStateRevision(1), profile, configuration,
            TestFactory.Authorization(
                context.AgentId, context.SessionId, inRunCorrelation, context.Identity),
            DateTimeOffset.UnixEpoch.AddSeconds(2), new IdempotencyKey(idempotencyKey));
    }

    private static SessionInputAdmissionRequest Admission(
        SessionOperationContext context,
        AdmissionId admissionId,
        InputId inputId,
        SessionEntryId entryId,
        SessionVersion version,
        SessionLaneRevision laneRevision,
        SessionBranchCursor cursor,
        string key)
    {
        var input = new AgentInput(
            inputId, InputDelivery.FollowUp,
            [new TextPart(key, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new SessionInputAdmissionRequest(
            context, admissionId, entryId, input, input,
            new InputPreprocessingManifest(
                new ConfigurationVersion(1), new InputFingerprint($"sha256:{key}:original"),
                new InputFingerprint($"sha256:{key}:effective")),
            DateTimeOffset.UnixEpoch.AddSeconds(1), version, laneRevision, cursor,
            new IdempotencyKey(key), 8);
    }
}
