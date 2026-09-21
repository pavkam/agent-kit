// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.IO;
using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.Session;
using AgentKit.TestSupport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

/// <summary>Composes <see cref="SessionBackedInputQueue"/> over one concrete session store and authorizes coordinator calls.</summary>
/// <remarks>
/// <para>
/// Subclasses register only their store and directory. This type owns session creation, lane acceptance, the
/// security stand-in used by <see cref="DefaultSessionCoordinator"/>, and resolution of <see cref="IInputQueue"/>
/// from a scope that also supplies the compiled <see cref="SessionExecutionCapability"/>. Grants are registered
/// with the same in-memory grant store the selected session store enforces, so admission and promotion are not
/// authorized by bypassing the coordinator.
/// </para>
/// <para>
/// The composed queue's pending ceiling is two. That bound is an input option, not a store-private knob, and it
/// is what <see cref="IInputQueueConformanceFixture.MaximumPendingInputsPerLane"/> reports.
/// </para>
/// </remarks>
public abstract class SessionBackedInputQueueConformanceFixture:
    IInputQueueConformanceFixture,
    ISecurityProfileSelector,
    ISecurityAuthoritySelector,
    ISecurityAuthority,
    ISecurityAuditDispatcher
{
    private const int PendingCapacity = 2;

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UnixEpoch);
    private readonly ExecutionIdentity _identity = TestExecutionIdentity.Create(
        new TenantId("tenant-1"), new PrincipalId("user-1"), ExecutionSubjectKind.Human);
    private long _nextIdentity;
    private ServiceProvider? _services;
    private IServiceScope? _scope;
    private ISecurityGrantStore? _grants;
    private ISessionCoordinator? _coordinator;
    private SessionProfileSnapshot? _profile;
    private SessionExecutionCapability? _capability;
    private IInputQueue? _queue;
    private AcceptedLane? _primary;
    private AcceptedLane? _secondary;

    /// <inheritdoc/>
    public int MaximumPendingInputsPerLane => PendingCapacity;

    /// <summary>Gets the store key the session profile must select.</summary>
    /// <value>The descriptor key published by the store registered in <see cref="RegisterStore"/>.</value>
    protected abstract string StoreKey { get; }

    /// <summary>Registers the concrete session store and its directory through their public extensions.</summary>
    /// <param name="services">The collection already containing session coordination and security replacements.</param>
    protected abstract void RegisterStore(IServiceCollection services);

    /// <summary>Initializes a store that cannot serve calls until an explicit bootstrap step runs.</summary>
    /// <param name="services">The built provider whose store and directory are already constructed.</param>
    /// <param name="cancellationToken">Cancels before initialization finishes.</param>
    /// <returns>A completed task when the selected store needs no extra bootstrap.</returns>
    protected virtual ValueTask PrepareStoreAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases fixture-owned persistence roots after the provider has been disposed.</summary>
    /// <returns>A completed task when the fixture owns no filesystem root.</returns>
    protected virtual ValueTask ReleaseStoreAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask<IInputQueue> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAsync(cancellationToken).ConfigureAwait(false);
        return _queue ?? throw new InvalidOperationException("The input queue was not composed.");
    }

    /// <inheritdoc/>
    public async ValueTask<InputAdmissionRequest> CreateAdmissionRequestAsync(
        AgentInput input,
        InputQueueConformanceLane lane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfUndefined(lane);
        await EnsureAsync(cancellationToken).ConfigureAwait(false);
        var accepted = Lane(lane);
        var authorization = Authorization(
            accepted.AgentId, accepted.SessionId, accepted.AdmissionCorrelation, accepted.Identity);
        return new InputAdmissionRequest(
            accepted.AgentId, accepted.SessionId, accepted.LaneId, accepted.Identity,
            accepted.AdmissionCorrelation, authorization, input);
    }

    /// <inheritdoc/>
    public async ValueTask<InputPromotionRequest> CreatePromotionRequestAsync(
        SessionSequence cutoffSequence,
        int maximumPromotions,
        InputQueueConformanceLane lane,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(lane);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        await EnsureAsync(cancellationToken).ConfigureAwait(false);
        var accepted = Lane(lane);
        var coordinator = _coordinator ?? throw new InvalidOperationException("The session coordinator was not composed.");
        var capability = _capability ?? throw new InvalidOperationException("The session capability was not compiled.");
        var beforeAuthorization = Authorization(
            accepted.AgentId, accepted.SessionId, accepted.AdmissionCorrelation, accepted.Identity);
        var before = new SessionOperationContext(
            accepted.AgentId, accepted.SessionId, accepted.LaneId, accepted.AdmissionCorrelation,
            accepted.Identity, beforeAuthorization);
        var laneState = (await coordinator.LoadLaneStateAsync(
            new SessionLaneStateRequest(before), capability, cancellationToken).ConfigureAwait(false))
            .ShouldBeOfType<SessionLaneStateLoaded>().State;
        var inRun = new SessionOperationContext(
            accepted.AgentId, accepted.SessionId, accepted.LaneId, accepted.Operation, accepted.Identity,
            accepted.InRunAuthorization);
        var run = (await coordinator.LoadRunStateAsync(
            new SessionRunStateRequest(inRun), capability, cancellationToken).ConfigureAwait(false))
            .ShouldBeOfType<SessionRunStateLoaded>().State;
        return new InputPromotionRequest(
            accepted.AgentId, accepted.SessionId, accepted.LaneId, run.Correlation, run.OperationStateRevision,
            laneState.BranchCursor, cutoffSequence, expectedVersion: null, expectedFencingToken: null,
            run.Identity, run.Authorization, PromotionBoundary.AfterTurnCommitted, run.InitialTurnId,
            new TurnId(NextGuid()), maximumPromotions);
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
            new SecurityAuthorizationContext(
                request.ProfileKey, new SecurityProfileVersion(1),
                new SecurityPolicySnapshotReference(
                    new SecurityPolicySnapshotId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                    new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
                new ComponentKey<ISecurityAuthority>("authority"), request.AgentDefinitionRevision,
                request.ConfigurationVersion, request.Scope, request.Identity)));
    }

    /// <inheritdoc/>
    ValueTask<SecurityAuthoritySelectionResult> ISecurityAuthoritySelector.SelectAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthoritySelectionResult>(
            new SecurityAuthoritySelected(authorization, this));
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = request.Authorization
            ?? throw new InvalidOperationException("Session coordinator requests retain captured authorization.");
        var grant = new SecurityGrant(
            new GrantId(NextGuid()), request.Id, request.Scope, request.Identity, authorization, request.Audience,
            request.Kind, request.Effect, request.Resources, request.InputFingerprint,
            authorization.PolicySnapshot.Version, new SecurityRevocationVersion(1), _clock.GetUtcNow(),
            request.Deadline, 1);
        await (_grants ?? throw new InvalidOperationException("The fixture has not composed grants."))
            .RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, authorization.PolicySnapshot.Version, grant);
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _scope?.Dispose();
        if (_services is not null)
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }

        await ReleaseStoreAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask EnsureAsync(CancellationToken cancellationToken)
    {
        if (_queue is not null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _services ??= BuildServices();
        _grants = _services.GetRequiredService<ISecurityGrantStore>();
        await PrepareStoreAsync(_services, cancellationToken).ConfigureAwait(false);
        _coordinator = _services.GetRequiredService<ISessionCoordinator>();
        var runCoordinator = _services.GetRequiredService<ISessionRunCoordinator>();
        _profile = TestSecurityEvidence.SessionProfile(StoreKey);
        _capability = new SessionExecutionCapability(_profile, _coordinator, runCoordinator);
        var createCorrelation = new BeforeRunOperationCorrelation(new OperationId(NextGuid()), null);
        var agentId = new AgentId(NextGuid());
        var created = (await _coordinator.CreateAsync(new SessionCreateRequest(
            agentId, _identity,
            Authorization(agentId, null, createCorrelation, _identity),
            conversationId: null, new IdempotencyKey("conformance-create"), ExtensionData.Empty),
            _profile, cancellationToken).ConfigureAwait(false)).ShouldBeOfType<SessionCreated>().Descriptor;
        _primary = await AcceptLaneAsync(
            created, new ExecutionLaneId(NextGuid()),
            new SessionBranchCursor(created.ActiveBranchId, null), "primary", cancellationToken)
            .ConfigureAwait(false);
        var branchCorrelation = new BeforeRunOperationCorrelation(new OperationId(NextGuid()), null);
        var branchContext = new SessionOperationContext(
            created.Address.AgentId, created.Address.SessionId, executionLaneId: null, branchCorrelation, _identity,
            Authorization(
                created.Address.AgentId, created.Address.SessionId, branchCorrelation, _identity));
        var branched = (await _coordinator.BranchAsync(new SessionBranchRequest(
            branchContext, created.ActiveBranchId, new SessionSequence(0), new IdempotencyKey("secondary-branch")),
            _profile, cancellationToken).ConfigureAwait(false)).ShouldBeOfType<SessionBranched>();
        _secondary = await AcceptLaneAsync(
            created, new ExecutionLaneId(NextGuid()),
            new SessionBranchCursor(branched.NewBranchId, null), "secondary", cancellationToken)
            .ConfigureAwait(false);
        _scope = _services.CreateScope();
        _queue = _scope.ServiceProvider.GetRequiredService<IInputQueue>();
    }

    private async Task<AcceptedLane> AcceptLaneAsync(
        SessionDescriptor created,
        ExecutionLaneId laneId,
        SessionBranchCursor cursor,
        string suffix,
        CancellationToken cancellationToken)
    {
        var coordinator = _coordinator ?? throw new InvalidOperationException("The session coordinator was not composed.");
        var capability = _capability ?? throw new InvalidOperationException("The session capability was not compiled.");
        var profile = _profile ?? throw new InvalidOperationException("The session profile was not compiled.");
        var correlation = new BeforeRunOperationCorrelation(new OperationId(NextGuid()), null);
        var before = LaneContext(created.Address, laneId, correlation);
        var version = await LoadVersionAsync(before, cancellationToken).ConfigureAwait(false);
        var configuration = new RunConfigurationReference(
            new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash("sha256:configuration"));
        var provisioned = (await coordinator.ProvisionLaneAsync(new SessionExecutionLaneProvisionRequest(
            before, cursor, version, new SessionEntryId(NextGuid()), profile.Reference, configuration,
            _clock.GetUtcNow(), new IdempotencyKey($"provision-{suffix}")), capability, cancellationToken)
            .ConfigureAwait(false)).ShouldBeOfType<SessionExecutionLaneProvisioned>();
        var input = new AgentInput(
            new InputId(NextGuid()), InputDelivery.FollowUp,
            [new TextPart($"initiate-{suffix}", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var fingerprint = InputPayloadFingerprint.Create(input);
        var preprocessing = new InputPreprocessingManifest(new ConfigurationVersion(1), fingerprint, fingerprint);
        var admissionId = new AdmissionId(NextGuid());
        var admitted = (await coordinator.AdmitInputAsync(new SessionInputAdmissionRequest(
            before, admissionId, new SessionEntryId(NextGuid()), input, input, preprocessing, _clock.GetUtcNow(),
            provisioned.SessionVersion, provisioned.LaneRevision, provisioned.BranchCursor,
            new IdempotencyKey($"admit-{suffix}"), maximumPendingInputs: 8), capability, cancellationToken)
            .ConfigureAwait(false)).ShouldBeOfType<AcceptedInput>();
        var laneState = (await coordinator.LoadLaneStateAsync(
            new SessionLaneStateRequest(before), capability, cancellationToken).ConfigureAwait(false))
            .ShouldBeOfType<SessionLaneStateLoaded>().State;
        var sessionVersion = await LoadVersionAsync(before, cancellationToken).ConfigureAwait(false);
        var runId = new RunId(NextGuid());
        var turnId = new TurnId(NextGuid());
        var operation = new InRunOperationCorrelation(correlation.OperationId, runId, turnId);
        var inRunAuthorization = Authorization(
            created.Address.AgentId, created.Address.SessionId, operation, _identity);
        var accepted = await coordinator.AcceptRunAsync(new SessionRunStartRequest(
            before, admissionId, [admissionId], admitted.Receipt.AdmittedSequence, laneState.Revision, sessionVersion,
            laneState.BranchCursor, expectedFencingToken: null, runId, turnId, new SessionEntryId(NextGuid()),
            [new SessionEntryId(NextGuid())], [new MessageId(NextGuid())], new SessionEntryId(NextGuid()),
            new OperationStateRevision(1), profile.Reference, configuration, inRunAuthorization, _clock.GetUtcNow(),
            new IdempotencyKey($"accept-{suffix}")), capability, cancellationToken).ConfigureAwait(false);
        _ = accepted.ShouldBeOfType<SessionRunAccepted>();
        return new AcceptedLane(
            laneId, correlation, operation, _identity, inRunAuthorization, created.Address.AgentId,
            created.Address.SessionId);
    }

    private async Task<SessionVersion> LoadVersionAsync(
        SessionOperationContext context,
        CancellationToken cancellationToken)
    {
        var coordinator = _coordinator ?? throw new InvalidOperationException("The session coordinator was not composed.");
        var profile = _profile ?? throw new InvalidOperationException("The session profile was not compiled.");
        var loaded = await coordinator.LoadAsync(context, profile, cancellationToken).ConfigureAwait(false);
        return loaded.ShouldBeOfType<SessionLoaded>().Descriptor.Version;
    }

    private static SecurityAuthorizationContext Authorization(
        AgentId agentId,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity) => new(
        new SecurityProfileKey("security"), new SecurityProfileVersion(1),
        new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new SecurityPolicyVersion(1), new ContentHash("sha256:policy")),
        new ComponentKey<ISecurityAuthority>("authority"), new AgentDefinitionRevision(1),
        new ConfigurationVersion(1), new SecurityAuthorizationScope(agentId, sessionId, correlation), identity);

    private SessionOperationContext LaneContext(
        SessionAddress address,
        ExecutionLaneId laneId,
        OperationCorrelation correlation) => new(
        address.AgentId, address.SessionId, laneId, correlation, _identity,
        Authorization(address.AgentId, address.SessionId, correlation, _identity));

    private AcceptedLane Lane(InputQueueConformanceLane lane) => lane switch
    {
        InputQueueConformanceLane.Primary => _primary
            ?? throw new InvalidOperationException("The primary lane has not been accepted."),
        InputQueueConformanceLane.Secondary => _secondary
            ?? throw new InvalidOperationException("The secondary lane has not been accepted."),
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "The lane is not one of the prepared lanes."),
    };

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<TimeProvider>(_clock);
        _ = services.AddAgentPermissions();
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddAgentSession();
        RegisterStore(services);
        _ = services.RemoveAll<ISecurityAuditDispatcher>();
        _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
        _ = services.RemoveAll<ISecurityProfileSelector>();
        _ = services.AddSingleton<ISecurityProfileSelector>(this);
        _ = services.RemoveAll<ISecurityAuthoritySelector>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(this);
        _ = services.AddScoped(_ => _capability
            ?? throw new InvalidOperationException("The fixture must accept a lane before resolving the queue."));
        _ = services.AddSessionBackedInputQueue(options => options.MaximumPendingInputsPerLane = PendingCapacity);
        _ = services.AddAgentIO(
            new ComponentKey<IInputCoordinator>("conformance-input"),
            new ComponentKey<IOutputPublisher>("conformance-output"),
            options => options.MaximumPendingInputsPerLane = PendingCapacity);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 1;
        return new Guid(bytes);
    }

    private sealed record AcceptedLane(
        ExecutionLaneId LaneId,
        BeforeRunOperationCorrelation AdmissionCorrelation,
        InRunOperationCorrelation Operation,
        ExecutionIdentity Identity,
        SecurityAuthorizationContext InRunAuthorization,
        AgentId AgentId,
        SessionId SessionId);
}
