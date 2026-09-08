// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using AgentKit.Conformance;
using AgentKit.Permissions;
using AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

/// <summary>Composes the default local run coordinator over the real protected in-memory session transaction path.</summary>
public sealed class InMemorySessionRunCoordinatorConformanceFixture:
    ISessionRunCoordinatorConformanceFixture,
    ISecurityProfileSelector,
    ISecurityAuthoritySelector,
    ISecurityAuthority,
    ISecurityAuditDispatcher
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UnixEpoch);
    private ServiceProvider? _services;
    private ISecurityGrantStore? _grants;

    /// <inheritdoc/>
    public async ValueTask<SessionRunCoordinatorConformanceScenario> CreateAcceptedScenarioAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _services ??= BuildServices();
        _grants = _services.GetRequiredService<ISecurityGrantStore>();
        var coordinator = _services.GetRequiredService<ISessionCoordinator>();
        var profile = TestSecurityEvidence.SessionProfile("agentkit.in-memory");
        var runCoordinator = _services.GetRequiredService<ISessionRunCoordinator>();
        var session = new SessionExecutionCapability(profile, coordinator, runCoordinator);
        var createRequest = TestFactory.CreateRequest(idempotencyKey: new IdempotencyKey("conformance-create"));
        var descriptor = (await coordinator.CreateAsync(createRequest, profile, cancellationToken))
            .ShouldBeOfType<SessionCreated>().Descriptor;
        var laneId = new ExecutionLaneId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var before = TestFactory.LaneContext(descriptor.Address, laneId,
            createRequest.Authorization.Scope.Correlation, createRequest.Identity);
        var configuration = new RunConfigurationReference(new ConfigurationVersion(1),
            new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provision = new SessionExecutionLaneProvisionRequest(before,
            new SessionBranchCursor(descriptor.ActiveBranchId, null), descriptor.Version,
            new SessionEntryId(Guid.Parse("10000000-0000-0000-0000-000000000002")),
            profile.Reference, configuration, _clock.GetUtcNow(), new IdempotencyKey("provision"));
        var provisioned = (await coordinator.ProvisionLaneAsync(provision, session, cancellationToken))
            .ShouldBeOfType<SessionExecutionLaneProvisioned>();
        var input = new AgentInput(new InputId(Guid.Parse("10000000-0000-0000-0000-000000000003")),
            InputDelivery.FollowUp, [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var preprocessing = new InputPreprocessingManifest(new ConfigurationVersion(1),
            new InputFingerprint("sha256:original"), new InputFingerprint("sha256:effective"));
        var lookup = new SessionInputLookupRequest(before, input, preprocessing.OriginalFingerprint);
        _ = (await coordinator.LookupInputAsync(lookup, session, cancellationToken))
            .ShouldBeOfType<SessionInputNotFound>();
        var admission = new SessionInputAdmissionRequest(before,
            new AdmissionId(Guid.Parse("10000000-0000-0000-0000-000000000004")),
            new SessionEntryId(Guid.Parse("10000000-0000-0000-0000-000000000005")), input, input,
            preprocessing, _clock.GetUtcNow(), provisioned.SessionVersion, provisioned.LaneRevision,
            provisioned.BranchCursor, new IdempotencyKey("admit"), 8);
        var admitted = (await coordinator.AdmitInputAsync(admission, session, cancellationToken))
            .ShouldBeOfType<AcceptedInput>();
        _ = (await coordinator.LookupInputAsync(lookup, session, cancellationToken))
            .ShouldBeOfType<SessionInputReplayFound>();
        var runId = new RunId(Guid.Parse("10000000-0000-0000-0000-000000000006"));
        var turnId = new TurnId(Guid.Parse("10000000-0000-0000-0000-000000000007"));
        var correlation = new InRunOperationCorrelation(before.Correlation.OperationId, runId, turnId);
        var inRunAuthorization = TestFactory.Authorization(descriptor.Address.AgentId,
            descriptor.Address.SessionId, correlation, createRequest.Identity);
        var start = new SessionRunStartRequest(before, admission.AdmissionId, [admission.AdmissionId],
            admitted.Receipt.AdmittedSequence, new SessionLaneRevision(provisioned.LaneRevision.Value + 1),
            new SessionVersion(provisioned.SessionVersion.Value + 1),
            new SessionBranchCursor(descriptor.ActiveBranchId, admission.EntryId), null, runId, turnId,
            new SessionEntryId(Guid.Parse("10000000-0000-0000-0000-000000000008")),
            [new SessionEntryId(Guid.Parse("10000000-0000-0000-0000-000000000009"))],
            [new MessageId(Guid.Parse("10000000-0000-0000-0000-000000000010"))],
            new SessionEntryId(Guid.Parse("10000000-0000-0000-0000-000000000011")),
            new OperationStateRevision(1), profile.Reference, configuration, inRunAuthorization,
            _clock.GetUtcNow(), new IdempotencyKey("accept"));
        var accepted = (await coordinator.AcceptRunAsync(start, session, cancellationToken))
            .ShouldBeOfType<SessionRunAccepted>().State;
        var inRunContext = TestFactory.LaneContext(descriptor.Address, laneId, correlation,
            createRequest.Identity);
        var loaded = (await coordinator.LoadRunStateAsync(new SessionRunStateRequest(inRunContext),
            session, cancellationToken)).ShouldBeOfType<SessionRunStateLoaded>().State;
        loaded.ShouldBe(accepted);
        var request = new SessionRunLeaseRequest(inRunContext, accepted.OperationStateRevision);
        return new SessionRunCoordinatorConformanceScenario(runCoordinator, request, session, accepted);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionRunCoordinatorConformancePair> CreateDifferentLaneScenariosAsync(
        CancellationToken cancellationToken = default)
    {
        var first = await CreateAcceptedScenarioAsync(cancellationToken).ConfigureAwait(false);
        var second = CreatePeer(first, first.Request.Context.Identity,
            new ExecutionLaneId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            Guid.Parse("20000000-0000-0000-0000-000000000010"));
        return BindPair(first, second);
    }

    /// <inheritdoc/>
    public async ValueTask<SessionRunCoordinatorConformancePair> CreateDifferentTenantScenariosAsync(
        CancellationToken cancellationToken = default)
    {
        var first = await CreateAcceptedScenarioAsync(cancellationToken).ConfigureAwait(false);
        var second = CreatePeer(first, TestFactory.Identity("tenant-2"),
            first.Request.ExecutionLaneId, Guid.Parse("30000000-0000-0000-0000-000000000010"));
        return BindPair(first, second);
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
            new SecurityAuthorizationContext(request.ProfileKey, new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
                new ComponentKey<ISecurityAuthority>("authority"), request.AgentDefinitionRevision,
                request.ConfigurationVersion, request.Scope, request.Identity)));
    }

    /// <inheritdoc/>
    ValueTask<SecurityAuthoritySelectionResult> ISecurityAuthoritySelector.SelectAsync(
        SecurityAuthorizationContext authorization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
            new SecurityAuthoritySelected(authorization, this));
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = request.Authorization
            ?? throw new InvalidOperationException("Session coordinator requests retain captured authorization.");
        var grant = new SecurityGrant(new GrantId(Guid.NewGuid()), request.Id, request.Scope,
            request.Identity, authorization, request.Audience, request.Kind, request.Effect,
            request.Resources, request.InputFingerprint, authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1), _clock.GetUtcNow(), request.Deadline, 1);
        await (_grants ?? throw new InvalidOperationException("The fixture has not composed grants."))
            .RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, authorization.PolicySnapshot.Version, grant);
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _services?.Dispose();
        return ValueTask.CompletedTask;
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<TimeProvider>(_clock);
        _ = services.AddAgentPermissions();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionDirectory(new ComponentId("conformance-directory"));
        _ = services.RemoveAll<ISecurityAuditDispatcher>();
        _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
        _ = services.RemoveAll<ISecurityProfileSelector>();
        _ = services.AddSingleton<ISecurityProfileSelector>(this);
        _ = services.RemoveAll<ISecurityAuthoritySelector>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(this);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static SessionRunCoordinatorConformancePair BindPair(
        SessionRunCoordinatorConformanceScenario first,
        SessionRunCoordinatorConformanceScenario second)
    {
        var stateCoordinator = new PairStateCoordinator(first.AcceptedState, second.AcceptedState);
        var firstCapability = new SessionExecutionCapability(first.Session.Profile,
            stateCoordinator, first.Coordinator);
        var secondCapability = new SessionExecutionCapability(second.Session.Profile,
            stateCoordinator, second.Coordinator);
        return new SessionRunCoordinatorConformancePair(
            first with { Session = firstCapability },
            second with { Session = secondCapability });
    }

    private static SessionRunCoordinatorConformanceScenario CreatePeer(
        SessionRunCoordinatorConformanceScenario first,
        ExecutionIdentity identity,
        ExecutionLaneId laneId,
        Guid identitySeed)
    {
        var operationId = new OperationId(identitySeed);
        var runId = new RunId(new Guid([.. identitySeed.ToByteArray().Select(static value => (byte) (value ^ 0x11))]));
        var turnId = new TurnId(new Guid([.. identitySeed.ToByteArray().Select(static value => (byte) (value ^ 0x22))]));
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        var context = TestFactory.LaneContext(first.AcceptedState.Address, laneId, correlation, identity);
        var request = new SessionRunLeaseRequest(context, first.AcceptedState.OperationStateRevision);
        var admissionId = new AdmissionId(new Guid([.. identitySeed.ToByteArray().Select(static value => (byte) (value ^ 0x33))]));
        var materializedEntryId = new SessionEntryId(
            new Guid([.. identitySeed.ToByteArray().Select(static value => (byte) (value ^ 0x44))]));
        var materializedMessageId = new MessageId(
            new Guid([.. identitySeed.ToByteArray().Select(static value => (byte) (value ^ 0x55))]));
        var state = new SessionAcceptedRunState(first.AcceptedState.Address, laneId,
            first.AcceptedState.LaneRevision, correlation, first.AcceptedState.OperationStateRevision,
            identity, context.Authorization, first.AcceptedState.SessionProfile,
            first.AcceptedState.Configuration, first.AcceptedState.PreviousCursor,
            first.AcceptedState.CommittedCursor, first.AcceptedState.PromotionCutoff,
            admissionId, [admissionId], [materializedEntryId], [materializedMessageId],
            turnId, first.AcceptedState.AcceptedAt);
        return new SessionRunCoordinatorConformanceScenario(first.Coordinator, request, first.Session, state);
    }

    private sealed class PairStateCoordinator(params SessionAcceptedRunState[] states): ISessionCoordinator
    {
        public ValueTask<SessionRunStateResult> LoadRunStateAsync(SessionRunStateRequest request,
            SessionExecutionCapability session, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(session);
            cancellationToken.ThrowIfCancellationRequested();
            var state = states.SingleOrDefault(candidate =>
                candidate.Address == request.Context.ToAddress()
                && candidate.ExecutionLaneId == request.Context.ExecutionLaneId
                && candidate.Correlation == request.Context.Correlation
                && candidate.Identity == request.Context.Identity);
            return ValueTask.FromResult<SessionRunStateResult>(state is null
                ? new SessionRunStateUnavailable("The conformance operation state is unavailable.")
                : new SessionRunStateLoaded(state));
        }

        public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request,
            SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context,
            SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request,
            SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request,
            SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request,
            SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request,
            SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
