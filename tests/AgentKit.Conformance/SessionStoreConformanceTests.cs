// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines reusable protected-store behavior for interchangeable <see cref="ISessionStore"/> implementations.</summary>
/// <typeparam name="TFixture">The fixture that composes one store and supplies exact fresh authority.</typeparam>
public abstract class SessionStoreConformanceTests<TFixture>
    where TFixture : ISessionStoreConformanceFixture
{
    /// <summary>Creates an isolated fixture for one conformance case.</summary>
    /// <returns>A new fixture with no retained session state or spent grants.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies changed protected bytes are denied before creation and do not consume the exact valid request's grant.</summary>
    [Fact]
    public async Task CreateAsync_WhenGrantDoesNotBindExactRequest_DeniesBeforeAccess()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateStoreRequest();
        var authorized = await fixture.AuthorizeAsync(
            request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
            TestContext.Current.CancellationToken);
        var changedLogical = new SessionCreateRequest(
            request.Request.AgentId, request.Request.Identity, request.Request.Authorization,
            request.Request.ConversationId, request.Request.IdempotencyKey,
            new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                "conformance.changed", new ExtensionValue([1]))));
        var changedRequest = new SessionStoreCreateRequest(changedLogical, request.Address, request.Context);
        var mismatched = new AuthorizedSessionStoreRequest<SessionStoreCreateRequest>(
            changedRequest, authorized.StoreKey, authorized.Grant, authorized.Intent);

        var denied = await store.CreateAsync(mismatched, TestContext.Current.CancellationToken);
        var accepted = await store.CreateAsync(authorized, TestContext.Current.CancellationToken);

        _ = denied.ShouldBeOfType<SessionCreateFailed>();
        ((SessionCreated) accepted).Descriptor.Address.ShouldBe(request.Address);
    }

    /// <summary>Verifies an exactly reconstructed creation retry returns the original session instead of allocating another.</summary>
    [Fact]
    public async Task CreateAsync_WhenExactRequestIsReplayed_ReturnsOriginalReceipt()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = CreateStoreRequest();

        var first = (SessionCreated) await store.CreateAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var rebuiltLogical = new SessionCreateRequest(
            request.Request.AgentId, request.Request.Identity, request.Request.Authorization,
            request.Request.ConversationId, request.Request.IdempotencyKey, request.Request.Extensions);
        var rebuiltContext = new SessionOperationContext(
            request.Context.AgentId, request.Context.SessionId, request.Context.ExecutionLaneId,
            request.Context.Correlation, request.Context.Identity, request.Context.Authorization);
        var rebuiltRequest = new SessionStoreCreateRequest(
            rebuiltLogical, new SessionAddress(request.Address.AgentId, request.Address.SessionId), rebuiltContext);
        var replay = (SessionCreated) await store.CreateAsync(
            await AuthorizeAsync(fixture, rebuiltRequest, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);

        replay.ShouldBe(first);
        replay.Descriptor.Address.ShouldBe(request.Address);
    }

    /// <summary>Verifies stale compare-and-swap rejection leaves the previously committed append as the only history.</summary>
    [Fact]
    public async Task AppendAsync_WhenExpectedVersionIsStale_DoesNotPartiallyMutateHistory()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(20));
        var firstEntry = MessageEntry(descriptor, 30, 1, "first");
        var firstRequest = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-first"), [firstEntry]);
        var first = (SessionAppended) await store.AppendAsync(
            await AuthorizeAsync(fixture, firstRequest, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var staleEntry = MessageEntry(descriptor, 31, 2, "stale");
        var staleRequest = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("append-stale"), [staleEntry]);

        var conflict = await store.AppendAsync(
            await AuthorizeAsync(fixture, staleRequest, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, context);

        first.NewVersion.Value.ShouldBe(descriptor.Version.Value + 1);
        _ = conflict.ShouldBeOfType<SessionAppendConflict>();
        page.Entries.ShouldBe([firstEntry]);
    }

    /// <summary>Verifies a caller cannot combine an issued old version with a later branch tip and present it as captured evidence.</summary>
    [Fact]
    public async Task ReadAsync_WhenSnapshotVersionAndUpperSequencePairWasNeverIssued_ReturnsTypedFailure()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var context = SessionContext(descriptor.Address, Identity(), Correlation(35));
        var emptyRead = new SessionReadRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), 1);
        var issued = (SessionPage) await store.ReadAsync(
            await AuthorizeAsync(fixture, emptyRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var entries = ImmutableArray.Create<SessionEntry>(
            MessageEntry(descriptor, 36, 1, "one"),
            MessageEntry(descriptor, 38, 2, "two"),
            MessageEntry(descriptor, 40, 3, "three"));
        var append = new SessionAppendRequest(
            context, descriptor.ActiveBranchId, descriptor.Version,
            new IdempotencyKey("append-after-snapshot"), entries);
        _ = await store.AppendAsync(
            await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var forged = new SessionReadSnapshot(
            descriptor.Address, descriptor.ActiveBranchId,
            issued.Snapshot!.Version, new SessionSequence(3));
        var continuedRead = new SessionReadRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), 10, forged);

        var result = await store.ReadAsync(
            await AuthorizeAsync(fixture, continuedRead, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SessionReadFailed>();
    }

    /// <summary>Verifies a fully authorized foreign tenant observes absence and cannot mutate the owner's record.</summary>
    [Fact]
    public async Task Operations_WhenTenantDiffers_MaskExistenceAndPreserveOwnerState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var descriptor = await CreateSessionAsync(fixture, store);
        var foreignIdentity = Identity("tenant-foreign", "foreign-user");
        var foreign = SessionContext(descriptor.Address, foreignIdentity, Correlation(40));
        var foreignEntry = MessageEntry(descriptor, 41, 1, "foreign");
        var append = new SessionAppendRequest(
            foreign, descriptor.ActiveBranchId, descriptor.Version, new IdempotencyKey("foreign-append"), [foreignEntry]);

        var loadResult = await store.LoadAsync(
            await AuthorizeAsync(fixture, foreign, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var appendResult = await store.AppendAsync(
            await AuthorizeAsync(fixture, append, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var ownerContext = SessionContext(descriptor.Address, Identity(), Correlation(42));
        var page = (SessionPage) await ReadAllAsync(fixture, store, descriptor, ownerContext);

        _ = loadResult.ShouldBeOfType<SessionNotFound>();
        _ = appendResult.ShouldBeOfType<SessionAppendNotFound>();
        page.Entries.ShouldBeEmpty();
    }

    /// <summary>Verifies lookup returns the durable original and effective payload before preprocessing is repeated.</summary>
    [Fact]
    public async Task LookupInputAsync_WhenInputWasAdmitted_ReturnsCompleteReplayBeforePreprocessing()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (_, context, _, admission, _) = await ProvisionAndAdmitAsync(fixture, store, 50, "lookup");
        var lookup = new SessionInputLookupRequest(
            context, admission.OriginalPayload,
            admission.Preprocessing.OriginalFingerprint);

        var result = await store.LookupInputAsync(
            await AuthorizeAsync(fixture, lookup, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);

        var replay = result.ShouldBeOfType<SessionInputReplayFound>();
        replay.AdmittedInput.OriginalPayload.ShouldBeEquivalentTo(admission.OriginalPayload);
        replay.AdmittedInput.EffectivePayload.ShouldBeEquivalentTo(admission.EffectivePayload);
        replay.AdmittedInput.Preprocessing.ShouldBe(admission.Preprocessing);
        replay.AdmittedInput.Identity.ShouldBe(context.Identity);
        replay.Receipt.Existing.ShouldBeTrue();
    }

    /// <summary>Verifies lane provisioning, admission, run acceptance, replay, and recovery retain one exact accepted state.</summary>
    [Fact]
    public async Task AcceptRunAsync_WhenPlanIsCurrent_CommitsAndRecoversCompleteAcceptedState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepared = await ProvisionAndAdmitAsync(fixture, store, 60, "accept");
        var start = StartRequest(prepared, 70);

        var accepted = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var replay = (SessionRunAccepted) await store.AcceptRunAsync(
            await AuthorizeAsync(fixture, start, SecurityOperationKind.StateMutation, SecurityEffect.Mutate),
            TestContext.Current.CancellationToken);
        var inRunContext = LaneContext(
            prepared.Descriptor.Address, prepared.Context.ExecutionLaneId!.Value,
            prepared.Context.Identity,
            new InRunOperationCorrelation(prepared.Context.Correlation.OperationId, start.RunId, start.InitialTurnId));
        var load = new SessionRunStateRequest(inRunContext);
        var loaded = (SessionRunStateLoaded) await store.LoadRunStateAsync(
            await AuthorizeAsync(fixture, load, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
        var page = (SessionPage) await ReadAllAsync(
            fixture, store, prepared.Descriptor, inRunContext);

        accepted.Existing.ShouldBeFalse();
        replay.Existing.ShouldBeTrue();
        replay.State.ShouldBeEquivalentTo(accepted.State);
        loaded.State.ShouldBeEquivalentTo(accepted.State);
        accepted.State.State.ShouldBe(DurableOperationState.Accepted);
        accepted.State.InitiatingAdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        loaded.State.InitiatingAdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        accepted.State.PromotedAdmissionIds.ShouldBe([prepared.Admission.AdmissionId]);
        accepted.State.Configuration.ShouldBe(start.Configuration);
        accepted.State.SessionProfile.ShouldBe(start.SessionProfile);
        page.Entries.OfType<InputPromotedSessionEntry>().Single()
            .InitiatingAdmissionId.ShouldBe(prepared.Admission.AdmissionId);
        page.Entries.Count(static entry => entry is MessageSessionEntry).ShouldBe(1);
        page.Entries[^1].ShouldBeOfType<OperationAcceptedSessionEntry>().State
            .ShouldBeEquivalentTo(accepted.State);
    }

    /// <summary>Verifies an admission identity collision leaves version, lane cursor, and sequence available to the next valid commit.</summary>
    [Fact]
    public async Task AdmitInputAsync_WhenAdmissionIdentityCollides_DoesNotAdvanceState()
    {
        await using var fixture = CreateFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var (descriptor, context, provisioned, admission, firstAccepted) =
            await ProvisionAndAdmitAsync(fixture, store, 80, "first");
        var currentVersion = new SessionVersion(provisioned.SessionVersion.Value + 1);
        var currentLaneRevision = new SessionLaneRevision(provisioned.LaneRevision.Value + 1);
        var currentCursor = new SessionBranchCursor(
            descriptor.ActiveBranchId, admission.EntryId);
        var collision = AdmissionRequest(
            context, admission.AdmissionId, Identifier<InputId>(90), Identifier<SessionEntryId>(91),
            currentVersion, currentLaneRevision, currentCursor, "collision");
        var subsequent = AdmissionRequest(
            context, Identifier<AdmissionId>(92), Identifier<InputId>(93), Identifier<SessionEntryId>(94),
            currentVersion, currentLaneRevision, currentCursor, "subsequent");

        var rejected = await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, collision, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        var accepted = await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, subsequent, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);

        _ = rejected.ShouldBeOfType<InputConflict>();
        accepted.ShouldBeOfType<AcceptedInput>().Receipt.AdmittedSequence.Value
            .ShouldBe(firstAccepted.Receipt.AdmittedSequence.Value + 1);
    }

    private static async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
        TFixture fixture, TRequest request, SecurityOperationKind kind, SecurityEffect effect)
        where TRequest : class =>
        await fixture.AuthorizeAsync(request, kind, effect, TestContext.Current.CancellationToken);

    private static async ValueTask<SessionDescriptor> CreateSessionAsync(TFixture fixture, ISessionStore store)
    {
        var request = CreateStoreRequest();
        var result = await store.CreateAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        return result.ShouldBeOfType<SessionCreated>().Descriptor;
    }

    private static async ValueTask<SessionPageResult> ReadAllAsync(
        TFixture fixture, ISessionStore store, SessionDescriptor descriptor, SessionOperationContext context)
    {
        var request = new SessionReadRequest(
            context, descriptor.ActiveBranchId, new SessionSequence(0), 100);
        return await store.ReadAsync(
            await AuthorizeAsync(fixture, request, SecurityOperationKind.StateRead, SecurityEffect.Observe),
            TestContext.Current.CancellationToken);
    }

    private static async ValueTask<(
        SessionDescriptor Descriptor,
        SessionOperationContext Context,
        SessionExecutionLaneProvisioned Provisioned,
        SessionInputAdmissionRequest Admission,
        AcceptedInput Accepted)> ProvisionAndAdmitAsync(
        TFixture fixture, ISessionStore store, int offset, string key)
    {
        var descriptor = await CreateSessionAsync(fixture, store);
        var identity = Identity();
        var laneId = Identifier<ExecutionLaneId>(offset);
        var context = LaneContext(
            descriptor.Address, laneId, identity,
            new BeforeRunOperationCorrelation(Identifier<OperationId>(offset + 1), null));
        var profile = Profile();
        var configuration = Configuration();
        var provision = new SessionExecutionLaneProvisionRequest(
            context, new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            Identifier<SessionEntryId>(offset + 2), profile, configuration,
            Timestamp(offset), new IdempotencyKey($"{key}-provision"));
        var provisioned = (SessionExecutionLaneProvisioned) await store.ProvisionLaneAsync(
            await AuthorizeAsync(fixture, provision, SecurityOperationKind.StateMutation, SecurityEffect.Create),
            TestContext.Current.CancellationToken);
        var admission = AdmissionRequest(
            context, Identifier<AdmissionId>(offset + 3), Identifier<InputId>(offset + 4),
            Identifier<SessionEntryId>(offset + 5), provisioned.SessionVersion,
            provisioned.LaneRevision, provisioned.BranchCursor, key);
        var accepted = (AcceptedInput) await store.AdmitInputAsync(
            await AuthorizeAsync(fixture, admission, SecurityOperationKind.StateMutation, SecurityEffect.Append),
            TestContext.Current.CancellationToken);
        return (descriptor, context, provisioned, admission, accepted);
    }

    private static SessionStoreCreateRequest CreateStoreRequest()
    {
        var agentId = Identifier<AgentId>(1);
        var identity = Identity();
        var correlation = new BeforeRunOperationCorrelation(Identifier<OperationId>(2), null);
        var authorization = Authorization(agentId, null, correlation, identity);
        var logical = new SessionCreateRequest(
            agentId, identity, authorization, Identifier<ConversationId>(3),
            new IdempotencyKey("create"), ExtensionData.Empty);
        var address = new SessionAddress(agentId, Identifier<SessionId>(4));
        var context = new SessionOperationContext(
            address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));
        return new SessionStoreCreateRequest(logical, address, context);
    }

    private static SessionRunStartRequest StartRequest(
        (SessionDescriptor Descriptor, SessionOperationContext Context,
            SessionExecutionLaneProvisioned Provisioned, SessionInputAdmissionRequest Admission,
            AcceptedInput Accepted) prepared,
        int offset)
    {
        var runId = Identifier<RunId>(offset);
        var turnId = Identifier<TurnId>(offset + 1);
        var inRunCorrelation = new InRunOperationCorrelation(
            prepared.Context.Correlation.OperationId, runId, turnId);
        var inRunAuthorization = Authorization(
            prepared.Descriptor.Address.AgentId, prepared.Descriptor.Address.SessionId,
            inRunCorrelation, prepared.Context.Identity);
        return new SessionRunStartRequest(
            prepared.Context, prepared.Admission.AdmissionId, [prepared.Admission.AdmissionId],
            prepared.Accepted.Receipt.AdmittedSequence,
            new SessionLaneRevision(prepared.Provisioned.LaneRevision.Value + 1),
            new SessionVersion(prepared.Provisioned.SessionVersion.Value + 1),
            new SessionBranchCursor(prepared.Descriptor.ActiveBranchId, prepared.Admission.EntryId),
            null, runId, turnId, Identifier<SessionEntryId>(offset + 2),
            [Identifier<SessionEntryId>(offset + 3)], [Identifier<MessageId>(offset + 4)],
            Identifier<SessionEntryId>(offset + 5), new OperationStateRevision(1),
            Profile(), Configuration(), inRunAuthorization, Timestamp(offset),
            new IdempotencyKey("accept"));
    }

    private static SessionInputAdmissionRequest AdmissionRequest(
        SessionOperationContext context, AdmissionId admissionId, InputId inputId,
        SessionEntryId entryId, SessionVersion version, SessionLaneRevision laneRevision,
        SessionBranchCursor cursor, string key)
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
            context, admissionId, entryId, original, effective, preprocessing,
            Timestamp(entryId.Value.GetHashCode()), version, laneRevision, cursor,
            new IdempotencyKey($"{key}-admit"), 8);
    }

    private static MessageSessionEntry MessageEntry(
        SessionDescriptor descriptor, int offset, long sequence, string text)
    {
        var correlation = Correlation(offset);
        var message = new UserMessage(
            Identifier<MessageId>(offset + 1), descriptor.Address.AgentId,
            descriptor.Address.SessionId, descriptor.ConversationId, descriptor.ActiveBranchId,
            correlation.RunId, null, Timestamp(offset), MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        return new MessageSessionEntry(
            Identifier<SessionEntryId>(offset + 2), descriptor.Address, correlation,
            descriptor.ActiveBranchId, new SessionSequence(sequence), null, Timestamp(offset),
            new SchemaVersion("1"), message);
    }

    private static SessionOperationContext SessionContext(
        SessionAddress address, ExecutionIdentity identity, OperationCorrelation correlation) =>
        new(address.AgentId, address.SessionId, null, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));

    private static SessionOperationContext LaneContext(
        SessionAddress address, ExecutionLaneId laneId, ExecutionIdentity identity,
        OperationCorrelation correlation) =>
        new(address.AgentId, address.SessionId, laneId, correlation, identity,
            Authorization(address.AgentId, address.SessionId, correlation, identity));

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId, SessionId? sessionId, OperationCorrelation correlation,
        ExecutionIdentity identity) =>
        new(new SecurityProfileKey("conformance"), new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(Identifier<SecurityPolicySnapshotId>(5),
                new SecurityPolicyVersion(1), new ContentHash("sha256:conformance-policy")),
            new ComponentKey<ISecurityAuthority>("conformance"), new AgentDefinitionRevision(1),
            new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private static ExecutionIdentity Identity(
        string tenant = "tenant-owner", string principal = "owner") =>
        TestExecutionIdentity.Create(
            new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

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
            _ => throw new InvalidOperationException($"Unsupported conformance identifier type {typeof(T).FullName}."),
        };
    }
}
