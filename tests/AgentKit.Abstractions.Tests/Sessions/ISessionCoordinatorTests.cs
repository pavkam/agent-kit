// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit.TestSupport;

/// <summary>Verifies ISessionCoordinator behavior and contracts.</summary>
public sealed class ISessionCoordinatorTests
{
    [Fact]
    public void CoordinatorDefaultMethods_WhenReferenceIsNull_ThrowExactArgumentNullException()
    {
        ISessionCoordinator coordinator = new UnsupportedCoordinator();
        var requests = Requests(coordinator);
        Should.Throw<ArgumentNullException>(() => _ = coordinator.LookupInputAsync(null!, requests.Capability).AsTask()).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.ProvisionLaneAsync(null!, requests.Capability).AsTask()).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.AdmitInputAsync(null!, requests.Capability).AsTask()).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.AcceptRunAsync(null!, requests.Capability).AsTask()).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.LoadRunStateAsync(null!, requests.Capability).AsTask()).ParamName.ShouldBe("request");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.LookupInputAsync(requests.Lookup, null!).AsTask()).ParamName.ShouldBe("session");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.ProvisionLaneAsync(requests.Provision, null!).AsTask()).ParamName.ShouldBe("session");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.AdmitInputAsync(requests.Admission, null!).AsTask()).ParamName.ShouldBe("session");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.AcceptRunAsync(requests.Start, null!).AsTask()).ParamName.ShouldBe("session");
        Should.Throw<ArgumentNullException>(() => _ = coordinator.LoadRunStateAsync(requests.Load, null!).AsTask()).ParamName.ShouldBe("session");
    }

    [Fact]
    public void CoordinatorDefaultMethods_WhenCallerAlreadyCancelled_PreserveCancellationToken()
    {
        ISessionCoordinator coordinator = new UnsupportedCoordinator();
        var requests = Requests(coordinator);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => _ = coordinator.LookupInputAsync(requests.Lookup, requests.Capability, cancellation.Token).AsTask()).CancellationToken.ShouldBe(cancellation.Token);
        Should.Throw<OperationCanceledException>(() => _ = coordinator.ProvisionLaneAsync(requests.Provision, requests.Capability, cancellation.Token).AsTask()).CancellationToken.ShouldBe(cancellation.Token);
        Should.Throw<OperationCanceledException>(() => _ = coordinator.AdmitInputAsync(requests.Admission, requests.Capability, cancellation.Token).AsTask()).CancellationToken.ShouldBe(cancellation.Token);
        Should.Throw<OperationCanceledException>(() => _ = coordinator.AcceptRunAsync(requests.Start, requests.Capability, cancellation.Token).AsTask()).CancellationToken.ShouldBe(cancellation.Token);
        Should.Throw<OperationCanceledException>(() => _ = coordinator.LoadRunStateAsync(requests.Load, requests.Capability, cancellation.Token).AsTask()).CancellationToken.ShouldBe(cancellation.Token);
    }

    private static SessionOperationContext Context(bool inRun, bool laneBound)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        OperationCorrelation correlation = inRun ? new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null) : new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null);
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        return new SessionOperationContext(agentId, sessionId, laneBound ? new ExecutionLaneId(Guid.NewGuid()) : null, correlation, identity, TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity));
    }

    private static RequestSet Requests(ISessionCoordinator coordinator)
    {
        var before = Context(inRun: false, laneBound: true);
        var profile = TestSecurityEvidence.SessionProfile();
        var configuration = new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var cursor = new SessionBranchCursor(new BranchId(Guid.NewGuid()), null);
        var input = new AgentInput(new InputId(Guid.NewGuid()), InputDelivery.FollowUp, [new TextPart("input", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective"));
        var admissionId = new AdmissionId(Guid.NewGuid());
        var lookup = new SessionInputLookupRequest(before, input, preprocessing.OriginalFingerprint);
        var provision = new SessionExecutionLaneProvisionRequest(before, cursor, new SessionVersion(0), new SessionEntryId(Guid.NewGuid()), profile.Reference, configuration, DateTimeOffset.UnixEpoch, new IdempotencyKey("provision"));
        var admission = new SessionInputAdmissionRequest(before, admissionId, new SessionEntryId(Guid.NewGuid()), input, input, preprocessing, DateTimeOffset.UnixEpoch, new SessionVersion(1), new SessionLaneRevision(2), cursor, new IdempotencyKey("admit"), 8);
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var inRunCorrelation = new InRunOperationCorrelation(before.Correlation.OperationId, runId, turnId);
        var inRunAuthorization = TestSecurityEvidence.Authorization(before.AgentId, before.SessionId, inRunCorrelation, before.Identity);
        var start = new SessionRunStartRequest(before, admissionId, [admissionId], new SessionSequence(1), new SessionLaneRevision(3), new SessionVersion(2), cursor, null, runId, turnId, new SessionEntryId(Guid.NewGuid()), [new SessionEntryId(Guid.NewGuid())], [new MessageId(Guid.NewGuid())], new SessionEntryId(Guid.NewGuid()), new OperationStateRevision(1), profile.Reference, configuration, inRunAuthorization, DateTimeOffset.UnixEpoch, new IdempotencyKey("start"));
        var inRun = new SessionOperationContext(before.AgentId, before.SessionId, before.ExecutionLaneId, inRunCorrelation, before.Identity, inRunAuthorization);
        var load = new SessionRunStateRequest(inRun);
        var runCoordinator = new UnsupportedRunCoordinator();
        return new RequestSet(lookup, provision, admission, start, load, new SessionExecutionCapability(profile, coordinator, runCoordinator));
    }

    private sealed record RequestSet(SessionInputLookupRequest Lookup, SessionExecutionLaneProvisionRequest Provision, SessionInputAdmissionRequest Admission, SessionRunStartRequest Start, SessionRunStateRequest Load, SessionExecutionCapability Capability);
    private sealed class UnsupportedCoordinator: ISessionCoordinator
    {
        public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnsupportedRunCoordinator: ISessionRunCoordinator
    {
        public ValueTask<SessionRunLeaseResult> AcquireAsync(SessionRunLeaseRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
