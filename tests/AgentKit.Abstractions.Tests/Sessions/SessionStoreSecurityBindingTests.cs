// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionStoreSecurityBinding behavior and contracts.</summary>
public sealed class SessionStoreSecurityBindingTests
{
    [Fact]
    public void FingerprintResource_WhenResourceIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SessionStoreSecurityBinding.FingerprintResource(null!)).ParamName.ShouldBe("payload");

    [Fact]
    public void FingerprintResource_WhenCalledTwiceWithSameResource_ProducesStableDigest()
    {
        var resource = new ProtectedResource(ProtectedResourceKind.ApplicationState, "session:store:test");
        var first = SessionStoreSecurityBinding.FingerprintResource(resource);
        var second = SessionStoreSecurityBinding.FingerprintResource(resource);
        first.ShouldBe(second);
    }

    [Fact]
    public void Resource_WhenArgumentsAreValid_ProducesExpectedResource()
    {
        var address = SessionsTestData.Address();
        var resource = SessionStoreSecurityBinding.Resource(new SessionStoreKey("store"), address);
        resource.ShouldBe(new ProtectedResource(ProtectedResourceKind.ApplicationState, $"session:store:{address.AgentId}:{address.SessionId}"));
    }

    [Fact]
    public void CreationResource_WhenArgumentsAreValid_ProducesExpectedResource()
    {
        var resource = SessionStoreSecurityBinding.CreationResource(new SessionStoreKey("store"), SessionsTestData.AgentId);
        resource.ShouldBe(new ProtectedResource(ProtectedResourceKind.ApplicationState, $"session-create:store:{SessionsTestData.AgentId}"));
    }

    [Fact]
    public void Fingerprint_WhenCalledWithEachRequestKind_ProducesStableDigest()
    {
        var createRequest = new SessionStoreCreateRequest(SessionsTestData.CreateRequest(), SessionsTestData.Address(), StoreCreateContext());
        var context = SessionsTestData.BeforeRunContext();
        var provisionRequest = new SessionExecutionLaneProvisionRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.Cursor(),
            new SessionVersion(1), SessionsTestData.EntryId, SessionsTestData.ProfileReference(), SessionsTestData.Configuration(),
            DateTimeOffset.UnixEpoch, new IdempotencyKey("provision"));
        var appendRequest = new SessionAppendRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.BranchId, new SessionVersion(0),
            new IdempotencyKey("append"), [MessageEntry()]);
        var readRequest = new SessionReadRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.BranchId, new SessionSequence(0), 10);
        var branchRequest = new SessionBranchRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.BranchId, new SessionSequence(1), new IdempotencyKey("branch"));
        var deleteRequest = new SessionDeleteRequest(SessionsTestData.BeforeRunContext(), new IdempotencyKey("delete"));
        var lookupRequest = new SessionInputLookupRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.Input(), new InputFingerprint("sha256:original"));
        var admissionRequest = new SessionInputAdmissionRequest(SessionsTestData.BeforeRunContext(), SessionsTestData.AdmissionId,
            SessionsTestData.EntryId, SessionsTestData.Input(), SessionsTestData.Input(), SessionsTestData.Preprocessing(),
            DateTimeOffset.UnixEpoch, new SessionVersion(1), new SessionLaneRevision(1), SessionsTestData.Cursor(), new IdempotencyKey("admit"), 8);
        var runStateRequest = new SessionRunStateRequest(SessionsTestData.InRunContext());
        var releaseRequest = new SessionRunReleaseRequest(SessionsTestData.InRunContext(), new OperationStateRevision(1), new SessionVersion(2), new IdempotencyKey("release"));

        SessionStoreSecurityBinding.Fingerprint(createRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(createRequest));
        SessionStoreSecurityBinding.Fingerprint(context).ShouldBe(SessionStoreSecurityBinding.Fingerprint(context));
        SessionStoreSecurityBinding.Fingerprint(provisionRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(provisionRequest));
        SessionStoreSecurityBinding.Fingerprint(appendRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(appendRequest));
        SessionStoreSecurityBinding.Fingerprint(readRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(readRequest));
        SessionStoreSecurityBinding.Fingerprint(branchRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(branchRequest));
        SessionStoreSecurityBinding.Fingerprint(deleteRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(deleteRequest));
        SessionStoreSecurityBinding.Fingerprint(lookupRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(lookupRequest));
        SessionStoreSecurityBinding.Fingerprint(admissionRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(admissionRequest));
        SessionStoreSecurityBinding.Fingerprint(runStateRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(runStateRequest));
        SessionStoreSecurityBinding.Fingerprint(releaseRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(releaseRequest));
    }

    [Fact]
    public void Fingerprint_WhenCalledWithRunStartRequest_ProducesStableDigest()
    {
        var runStartRequest = SessionsTestData.RunStartRequest();
        SessionStoreSecurityBinding.Fingerprint(runStartRequest).ShouldBe(SessionStoreSecurityBinding.Fingerprint(runStartRequest));
    }

    private static SessionOperationContext StoreCreateContext() =>
        new(SessionsTestData.AgentId, SessionsTestData.SessionId, null, SessionsTestData.BeforeRun(), SessionsTestData.Identity(),
            SessionsTestData.Authorization(SessionsTestData.BeforeRun(), SessionsTestData.SessionId));

    private static MessageSessionEntry MessageEntry() =>
        new(SessionsTestData.EntryId, SessionsTestData.Address(), SessionsTestData.BeforeRun(), SessionsTestData.BranchId,
            new SessionSequence(1), null, DateTimeOffset.UnixEpoch, new SchemaVersion("1"),
            new UserMessage(new MessageId(SessionsTestData.EntryId.Value), SessionsTestData.AgentId, SessionsTestData.SessionId, null,
                SessionsTestData.BranchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete,
                [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty));
}
