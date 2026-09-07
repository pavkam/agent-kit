// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>
/// Exercises the structural equality and remaining constructor guards of
/// session request/result/event records that are otherwise only ever
/// constructed once per test and compared by type, never for value
/// equality.
/// </summary>
public sealed class SessionHierarchyEqualityTests
{
    private static readonly Guid _agentGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _sessionGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _branchGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _operationGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _runGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid _entryGuid = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public void SessionOperationContext_Constructor_WhenCorrelationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new SessionOperationContext(AgentId, SessionId, null!, Identity()));

        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void SessionOperationContext_Constructor_WhenIdentityNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new SessionOperationContext(AgentId, SessionId, Correlation(), null!));

        exception.ParamName.ShouldBe("identity");
    }

    [Fact]
    public void SessionOperationContext_Equality_WhenSameValues_InstancesAreEqual() =>
        OperationContext().ShouldBe(OperationContext());

    [Fact]
    public void SessionOperationContext_ToAddress_ReturnsMatchingAddress()
    {
        var address = OperationContext().ToAddress();

        address.AgentId.ShouldBe(AgentId);
        address.SessionId.ShouldBe(SessionId);
    }

    [Fact]
    public void SessionAddress_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new SessionAddress(AgentId, SessionId);
        var second = new SessionAddress(AgentId, SessionId);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SessionAddress_Constructor_RoundTripsProperties()
    {
        var address = new SessionAddress(AgentId, SessionId);

        address.AgentId.ShouldBe(AgentId);
        address.SessionId.ShouldBe(SessionId);
    }

    [Fact]
    public void SessionCreateRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        CreateRequest().ShouldBe(CreateRequest());

    [Fact]
    public void SessionCreated_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionCreated(Descriptor(), existing: false).ShouldBe(new SessionCreated(Descriptor(), existing: false));

    [Fact]
    public void SessionLoaded_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionLoaded(Descriptor()).ShouldBe(new SessionLoaded(Descriptor()));

    [Fact]
    public void SessionNotFound_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionNotFound(Address()).ShouldBe(new SessionNotFound(Address()));

    [Fact]
    public void SessionAppendRequest_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = AppendRequest();
        var second = AppendRequest();

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SessionAppended_Equality_WhenSameValues_InstancesAreEqual()
    {
        var entries = ImmutableArray.Create<SessionEntry>(MessageEntry());
        var first = new SessionAppended(new SessionVersion(1), entries);
        var second = new SessionAppended(new SessionVersion(1), entries);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SessionPage_Equality_WhenSameValues_InstancesAreEqual()
    {
        var entries = ImmutableArray.Create<SessionEntry>(MessageEntry());
        var first = new SessionPage(entries, new SessionSequence(1), hasMore: false);
        var second = new SessionPage(entries, new SessionSequence(1), hasMore: false);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void SessionAppendConflict_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionAppendConflict(new SessionVersion(1), new SessionVersion(2)).ShouldBe(
            new SessionAppendConflict(new SessionVersion(1), new SessionVersion(2)));

    [Fact]
    public void SessionAppendNotFound_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionAppendNotFound(Address()).ShouldBe(new SessionAppendNotFound(Address()));

    [Fact]
    public void SessionReadRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        ReadRequest().ShouldBe(ReadRequest());

    [Fact]
    public void SessionReadNotFound_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionReadNotFound(Address()).ShouldBe(new SessionReadNotFound(Address()));

    [Fact]
    public void SessionBranchRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        BranchRequest().ShouldBe(BranchRequest());

    [Fact]
    public void SessionBranched_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionBranched(BranchId, new SessionSequence(1)).ShouldBe(new SessionBranched(BranchId, new SessionSequence(1)));

    [Fact]
    public void SessionBranchParentNotFound_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionBranchParentNotFound(BranchId, new SessionSequence(1)).ShouldBe(
            new SessionBranchParentNotFound(BranchId, new SessionSequence(1)));

    [Fact]
    public void SessionDeleteRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        DeleteRequest().ShouldBe(DeleteRequest());

    [Fact]
    public void SessionDeleted_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionDeleted(Address()).ShouldBe(new SessionDeleted(Address()));

    [Fact]
    public void SessionDescriptor_Constructor_WhenAddressNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDescriptor(
            null!,
            null,
            new TenantId("t"),
            new PrincipalId("p"),
            new SessionStoreKey("store"),
            BranchId,
            new SessionVersion(0),
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void SessionDescriptor_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionDescriptor(
            Address(),
            null,
            new TenantId("t"),
            new PrincipalId("p"),
            new SessionStoreKey("store"),
            BranchId,
            new SessionVersion(0),
            SessionLifecycleState.Active,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void SessionDescriptor_Equality_WhenSameValues_InstancesAreEqual() =>
        Descriptor().ShouldBe(Descriptor());

    [Fact]
    public void SessionEntry_Equality_WhenSameValues_InstancesAreEqual() =>
        MessageEntry().ShouldBe(MessageEntry());

    [Fact]
    public void MessageSessionEntry_Constructor_WhenMessageNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new MessageSessionEntry(
            new SessionEntryId(_entryGuid),
            Address(),
            Correlation(),
            BranchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            null!));

        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void SessionEvent_Hierarchy_EveryLeafDerivesFromSessionEvent()
    {
        SessionEvent created = new SessionCreatedEvent(Address(), DateTimeOffset.UnixEpoch, Descriptor());
        SessionEvent appended = new SessionAppendedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, new SessionVersion(1), 1);
        SessionEvent branched = new SessionBranchedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, BranchId, new SessionSequence(1));
        SessionEvent deleted = new SessionDeletedEvent(Address(), DateTimeOffset.UnixEpoch);

        _ = created.ShouldBeOfType<SessionCreatedEvent>();
        _ = appended.ShouldBeOfType<SessionAppendedEvent>();
        _ = branched.ShouldBeOfType<SessionBranchedEvent>();
        _ = deleted.ShouldBeOfType<SessionDeletedEvent>();
    }

    [Fact]
    public void SessionCreatedEvent_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionCreatedEvent(Address(), DateTimeOffset.UnixEpoch, Descriptor()).ShouldBe(
            new SessionCreatedEvent(Address(), DateTimeOffset.UnixEpoch, Descriptor()));

    [Fact]
    public void SessionAppendedEvent_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionAppendedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, new SessionVersion(1), 1).ShouldBe(
            new SessionAppendedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, new SessionVersion(1), 1));

    [Fact]
    public void SessionBranchedEvent_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionBranchedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, BranchId, new SessionSequence(1)).ShouldBe(
            new SessionBranchedEvent(Address(), DateTimeOffset.UnixEpoch, BranchId, BranchId, new SessionSequence(1)));

    [Fact]
    public void SessionDeletedEvent_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionDeletedEvent(Address(), DateTimeOffset.UnixEpoch).ShouldBe(
            new SessionDeletedEvent(Address(), DateTimeOffset.UnixEpoch));

    [Fact]
    public void SessionRetentionDecision_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionRetentionDecision(SessionRetentionAction.Delete, "old").ShouldBe(
            new SessionRetentionDecision(SessionRetentionAction.Delete, "old"));

    [Fact]
    public void SessionRunLeaseAcquired_Equality_WhenSameValues_InstancesAreEqual()
    {
        var lease = new FakeRunLease();

        new SessionRunLeaseAcquired(lease).ShouldBe(new SessionRunLeaseAcquired(lease));
    }

    [Fact]
    public void SessionRunBusy_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionRunBusy(new RunId(_runGuid)).ShouldBe(new SessionRunBusy(new RunId(_runGuid)));

    [Fact]
    public void SessionStoreDescriptor_Constructor_WhenDurableTrueOrFalse_RoundTrips()
    {
        var descriptor = new SessionStoreDescriptor(new SessionStoreKey("store"), durable: true);

        descriptor.Durable.ShouldBeTrue();
    }

    [Fact]
    public void SessionStoreDescriptor_Equality_WhenSameValues_InstancesAreEqual() =>
        new SessionStoreDescriptor(new SessionStoreKey("store"), true).ShouldBe(
            new SessionStoreDescriptor(new SessionStoreKey("store"), true));

    private static AgentId AgentId => new(_agentGuid);

    private static SessionId SessionId => new(_sessionGuid);

    private static BranchId BranchId => new(_branchGuid);

    private static ExecutionIdentity Identity() =>
        new(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human, ExtensionData.Empty);

    private static InRunOperationCorrelation Correlation() =>
        new(new OperationId(_operationGuid), new RunId(_runGuid), null);

    private static SessionAddress Address() => new(AgentId, SessionId);

    private static SessionOperationContext OperationContext() =>
        new(AgentId, SessionId, Correlation(), Identity());

    private static SessionCreateRequest CreateRequest() => new(
        AgentId, Identity(), null, new IdempotencyKey("key"), ExtensionData.Empty);

    private static SessionDescriptor Descriptor() => new(
        Address(),
        null,
        new TenantId("t"),
        new PrincipalId("p"),
        new SessionStoreKey("store"),
        BranchId,
        new SessionVersion(0),
        SessionLifecycleState.Active,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        ExtensionData.Empty);

    private static MessageSessionEntry MessageEntry() => new(
        new SessionEntryId(_entryGuid),
        Address(),
        Correlation(),
        BranchId,
        new SessionSequence(1),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        new UserMessage(
            new MessageId(_entryGuid),
            AgentId,
            SessionId,
            null,
            BranchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty));

    private static SessionAppendRequest AppendRequest() => new(
        OperationContext(), BranchId, new SessionVersion(0), new IdempotencyKey("key"), [MessageEntry()]);

    private static SessionReadRequest ReadRequest() => new(OperationContext(), BranchId, new SessionSequence(0), 10);

    private static SessionBranchRequest BranchRequest() => new(
        OperationContext(), BranchId, new SessionSequence(1), new IdempotencyKey("key"));

    private static SessionDeleteRequest DeleteRequest() => new(OperationContext(), new IdempotencyKey("key"));

    private sealed class FakeRunLease: ISessionRunLease
    {
        public RunId RunId => new(_runGuid);

        public SessionLeaseId LeaseId => throw new NotImplementedException();

        public AgentId AgentId => throw new NotImplementedException();

        public SessionId SessionId => throw new NotImplementedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
