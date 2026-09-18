// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using AgentKit.Conformance;

/// <summary>Runs the reusable protected session-store contract against the durable JSON adapter, plus its durability probes.</summary>
/// <remarks>
/// The shared suite never reopens a store, so the cases below cover the guarantee that is specific to this leaf: replaying
/// the newline-delimited record log must reproduce optimistic-concurrency versions, branch history, idempotency receipts,
/// per-lane ownership, and installed accepted run state exactly as they were before the process was lost.
/// </remarks>
public sealed class JsonSessionStoreTests: SessionStoreConformanceTests<JsonSessionStoreConformanceFixture>
{
    /// <inheritdoc/>
    protected override JsonSessionStoreConformanceFixture CreateFixture() => new();

    // ---- Durability probes beyond the shared suite. ----

    /// <summary>Verifies committed entries, the session version, and the append receipt all survive a reopen.</summary>
    [Fact]
    public async Task AppendAsync_WhenStoreIsReopened_ReplaysCommittedHistoryAndIdempotency()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(20));
        var entry = MessageEntry(descriptor, 30, 1, "durable");
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("durable"), [entry]);
        var first = (SessionAppended) await store.AppendAsync(
            await fixture.AuthorizeAsync(append, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var loaded = await reopened.LoadAsync(
            await fixture.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var page = await reopened.ReadAsync(
            await fixture.AuthorizeAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 100),
                SecurityOperationKind.StateRead, SecurityEffect.Observe, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var replay = await reopened.AppendAsync(
            await fixture.AuthorizeAsync(append, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version.ShouldBe(first.NewVersion);
        page.ShouldBeOfType<SessionPage>().Entries.ShouldBe([entry]);
        replay.ShouldBeOfType<SessionAppended>().NewVersion.ShouldBe(first.NewVersion);
    }

    /// <summary>Verifies a stale expected version observed before a reopen still conflicts after it.</summary>
    [Fact]
    public async Task AppendAsync_WhenExpectedVersionIsStaleAcrossReopen_ReturnsConflict()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = Context(descriptor.Address, null, Correlation(40));
        var committed = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("committed"),
            [MessageEntry(descriptor, 41, 1, "committed")]);
        _ = (await store.AppendAsync(
            await fixture.AuthorizeAsync(committed, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionAppended>();

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var stale = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("stale"),
            [MessageEntry(descriptor, 44, 2, "stale")]);
        var result = await reopened.AppendAsync(
            await fixture.AuthorizeAsync(stale, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<SessionAppendConflict>();
        conflict.ExpectedVersion.ShouldBe(descriptor.Version);
        conflict.ActualVersion.ShouldBe(new SessionVersion(descriptor.Version.Value + 1));
    }

    /// <summary>Verifies lane ownership and installed accepted run state survive a reopen and still fence a later start.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenStoreIsReopened_RetainsLaneOwnershipAndAcceptedState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var laneId = Identifier<ExecutionLaneId>(60);
        var laneContext = Context(descriptor.Address, laneId, new BeforeRunOperationCorrelation(
            Identifier<OperationId>(61), null));
        var provision = new SessionExecutionLaneProvisionRequest(
            laneContext, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(62), Profile(), Configuration(), Timestamp(60),
            new IdempotencyKey("provision"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(provision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var admission = AdmissionRequest(
            laneContext, Identifier<AdmissionId>(63), Identifier<InputId>(64), Identifier<SessionEntryId>(65),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor, "admit");
        var accepted = (AcceptedInput) await store.AdmitInputAsync(
            await fixture.AuthorizeAsync(admission, SecurityOperationKind.StateMutation, SecurityEffect.Append,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var runId = Identifier<RunId>(70);
        var turnId = Identifier<TurnId>(71);
        var start = new SessionRunStartRequest(
            laneContext, admission.AdmissionId, [admission.AdmissionId], accepted.Receipt.AdmittedSequence,
            new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionVersion(provisioned.SessionVersion.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), null, runId, turnId,
            Identifier<SessionEntryId>(72), [Identifier<SessionEntryId>(73)], [Identifier<MessageId>(74)],
            Identifier<SessionEntryId>(75), new OperationStateRevision(1), Profile(), Configuration(),
            Authorization(descriptor.Address.AgentId, descriptor.Address.SessionId,
                new InRunOperationCorrelation(laneContext.Correlation.OperationId, runId, turnId),
                laneContext.Identity),
            Timestamp(70), new IdempotencyKey("accept"));
        var started = (SessionRunAccepted) await store.AcceptRunAsync(
            await fixture.AuthorizeAsync(start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var inRunContext = Context(descriptor.Address, laneId, started.State.Correlation);
        var loaded = await reopened.LoadRunStateAsync(
            await fixture.AuthorizeAsync(new SessionRunStateRequest(inRunContext),
                SecurityOperationKind.StateRead, SecurityEffect.Observe, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var reprovision = new SessionExecutionLaneProvisionRequest(
            laneContext, started.State.CommittedCursor, started.SessionVersion, Identifier<SessionEntryId>(80),
            Profile(), Configuration(), Timestamp(80), new IdempotencyKey("reprovision"));
        var conflict = await reopened.ProvisionLaneAsync(
            await fixture.AuthorizeAsync(reprovision, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        loaded.ShouldBeOfType<SessionRunStateLoaded>().State.ShouldBeEquivalentTo(started.State);
        _ = conflict.ShouldBeOfType<SessionExecutionLaneProvisionConflict>();
    }

    /// <summary>Verifies a deleted session stays deleted and its creation retry key stays retired after a reopen.</summary>
    [Fact]
    public async Task DeleteAsync_WhenStoreIsReopened_KeepsSessionRemovedAndCreationKeyRetired()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var create = CreateStoreRequest();
        var descriptor = (await store.CreateAsync(
            await fixture.AuthorizeAsync(create, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionCreated>().Descriptor;
        var context = Context(descriptor.Address, null, Correlation(90));
        var delete = new SessionDeleteRequest(context, new IdempotencyKey("delete"));
        _ = (await store.DeleteAsync(
            await fixture.AuthorizeAsync(delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)).ShouldBeOfType<SessionDeleted>();

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);
        var loaded = await reopened.LoadAsync(
            await fixture.AuthorizeAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var recreated = await reopened.CreateAsync(
            await fixture.AuthorizeAsync(create, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        var repeatedDelete = await reopened.DeleteAsync(
            await fixture.AuthorizeAsync(delete, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        _ = loaded.ShouldBeOfType<SessionNotFound>();
        _ = recreated.ShouldBeOfType<SessionCreateFailed>();
        _ = repeatedDelete.ShouldBeOfType<SessionDeleted>();
    }

    private static async ValueTask<SessionDescriptor> CreateSessionAsync(
        JsonSessionStoreConformanceFixture fixture, ISessionStore store)
    {
        var request = CreateStoreRequest();
        var result = await store.CreateAsync(
            await fixture.AuthorizeAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        return result.ShouldBeOfType<SessionCreated>().Descriptor;
    }

    private static SessionStoreCreateRequest CreateStoreRequest()
    {
        var agentId = Identifier<AgentId>(1);
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
        var logical = new SessionCreateRequest(
            agentId, identity, Authorization(agentId, null, correlation, identity),
            Identifier<ConversationId>(3), new IdempotencyKey("create"), ExtensionData.Empty);
        var address = new SessionAddress(agentId, Identifier<SessionId>(4));
        var context = new SessionOperationContext(
            address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
        return new SessionStoreCreateRequest(logical, address, context);
    }

    private static SessionInputAdmissionRequest AdmissionRequest(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId, SessionEntryId entryId,
        SessionVersion version, SessionLaneRevision laneRevision, SessionBranchCursor cursor, string key)
    {
        var original = new AgentInput(
            inputId, InputDelivery.FollowUp,
            [new TextPart($"{key}-original", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var effective = new AgentInput(
            inputId, InputDelivery.FollowUp,
            [new TextPart($"{key}-effective", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(
            new ConfigurationVersion(1), new InputFingerprint($"sha256:{key}:original"),
            new InputFingerprint($"sha256:{key}:effective"));
        return new SessionInputAdmissionRequest(
            context, admissionId, entryId, original, effective, preprocessing, Timestamp(99), version,
            laneRevision, cursor, new IdempotencyKey($"{key}-admit"), 8);
    }

    private static MessageSessionEntry MessageEntry(
        SessionDescriptor descriptor, int offset, long sequence, string text)
    {
        var correlation = Correlation(offset);
        var message = new UserMessage(
            Identifier<MessageId>(offset + 1), descriptor.Address.AgentId, descriptor.Address.SessionId,
            descriptor.ConversationId, descriptor.ActiveBranchId, correlation.RunId, null, Timestamp(offset),
            MessageState.Complete, [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        return new MessageSessionEntry(
            Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation, descriptor.ActiveBranchId,
            new SessionSequence(sequence), null, Timestamp(offset), new SchemaVersion("1"), message);
    }

    private static SessionOperationContext Context(
        SessionAddress address, ExecutionLaneId? laneId, OperationCorrelation correlation) =>
        new(address.AgentId, address.SessionId, laneId, correlation, Identity(),
            Authorization(address.AgentId, address.SessionId, correlation, Identity()));

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation, ExecutionIdentity identity) =>
        new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
            new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private static ExecutionIdentity Identity() => TestExecutionIdentity.Create(
        new TenantId("tenant-owner"), new PrincipalId("owner"), ExecutionSubjectKind.Human);

    private static InRunOperationCorrelation Correlation(int offset) =>
        new(Identifier<OperationId>(offset), Identifier<RunId>(offset + 1), null);

    private static SessionProfileReference Profile() =>
        new(new SessionProfileKey("conformance"), new SessionProfileVersion(1));

    private static RunConfigurationReference Configuration() =>
        new(new ConfigurationVersion(1), new RunPolicyVersion(1),
            new ContentHash("sha256:conformance-configuration"));

    private static DateTimeOffset Timestamp(int offset) =>
        DateTimeOffset.UnixEpoch.AddSeconds(Math.Abs((long) offset) + 1);

    private static T Identifier<T>(int value)
    {
        var guid = new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        return typeof(T) switch
        {
            var type when type == typeof(AgentId) => (T) (object) new AgentId(guid),
            var type when type == typeof(SessionId) => (T) (object) new SessionId(guid),
            var type when type == typeof(ConversationId) => (T) (object) new ConversationId(guid),
            var type when type == typeof(OperationId) => (T) (object) new OperationId(guid),
            var type when type == typeof(RunId) => (T) (object) new RunId(guid),
            var type when type == typeof(TurnId) => (T) (object) new TurnId(guid),
            var type when type == typeof(ExecutionLaneId) => (T) (object) new ExecutionLaneId(guid),
            var type when type == typeof(SessionEntryId) => (T) (object) new SessionEntryId(guid),
            var type when type == typeof(MessageId) => (T) (object) new MessageId(guid),
            var type when type == typeof(AdmissionId) => (T) (object) new AdmissionId(guid),
            var type when type == typeof(InputId) => (T) (object) new InputId(guid),
            var type when type == typeof(SecurityPolicySnapshotId) =>
                (T) (object) new SecurityPolicySnapshotId(guid),
            _ => throw new InvalidOperationException($"Unsupported identifier type {typeof(T).FullName}."),
        };
    }
}
