// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Text;

using AgentKit.Internal;

/// <summary>
/// Package-internal lifecycle coordinator for one <see cref="AgentEngine"/>.
/// </summary>
/// <remarks>
/// <para>
/// The runtime owns the standalone or host-managed scope boundary. It is not a public extension point. Every
/// behavior it invokes is selected through the public abstractions, definitions, options, and dependency-injection
/// registrations the engine was composed with.
/// </para>
/// <para>
/// <c>RunAsync</c> and <c>StreamAsync</c> share one admission protocol with
/// <see cref="SendAsync"/>: pinned-definition revalidation, session ownership, an in-process busy gate, then the
/// durable lane sequence of provision, admit, accept, and release. The deleted process-local registry is not
/// reintroduced as a separate type.
/// </para>
/// </remarks>
internal sealed class AgentEngineRuntime
{
    private readonly Lock _disposeLock = new();
    private readonly IAsyncDisposable? _ownedProvider;
    private readonly IAgentDefinitionCatalog _catalog;
    private readonly IIdentifierGenerator<RunId> _runIds;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly IAgentRunProfilePublicationReader _runProfiles;
    private readonly ISecurityProfileSelector _securityProfiles;
    private readonly ImmutableDictionary<(AgentId, AgentDefinitionRevision), AgentRunProfilePublication> _pinnedRunProfiles;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly IIdentifierGenerator<TurnId> _turnIds;
    private readonly IIdentifierGenerator<AdmissionId> _admissionIds;
    private readonly IIdentifierGenerator<InputId> _inputIds;
    private readonly AgentRunScopeFactory _scopes;
    private readonly InProcessSessionGate _gate = new();
    private readonly ActiveRunRegistry _activeRuns = new();
    private readonly ILogger<AgentEngine> _logger;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>Initializes the runtime over one captured composition.</summary>
    /// <param name="services">The composition scoped run services are resolved from.</param>
    /// <param name="ownedProvider">
    /// The standalone provider this runtime owns, or <see langword="null"/> when an external host owns it.
    /// </param>
    /// <param name="validatedComposition">
    /// The exact readiness evidence composition validation already inspected. Construction pins it without
    /// consulting replaceable readers again.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="validatedComposition"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">The composition is missing an engine-wide service the facade requires.</exception>
    internal AgentEngineRuntime(
        IServiceProvider services,
        IAsyncDisposable? ownedProvider,
        AgentCompositionSnapshot validatedComposition)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validatedComposition);

        Services = services;
        _ownedProvider = ownedProvider;
        TimeProvider = services.GetRequiredService<TimeProvider>();
        _catalog = services.GetRequiredService<IAgentDefinitionCatalog>();
        _runIds = services.GetRequiredService<IIdentifierGenerator<RunId>>();
        _operationIds = services.GetRequiredService<IIdentifierGenerator<OperationId>>();
        _runProfiles = services.GetRequiredService<IAgentRunProfilePublicationReader>();
        _securityProfiles = services.GetRequiredService<ISecurityProfileSelector>();
        _messageIds = services.GetRequiredService<IIdentifierGenerator<MessageId>>();
        _entryIds = services.GetRequiredService<IIdentifierGenerator<SessionEntryId>>();
        _turnIds = services.GetRequiredService<IIdentifierGenerator<TurnId>>();
        _admissionIds = services.GetRequiredService<IIdentifierGenerator<AdmissionId>>();
        _inputIds = services.GetRequiredService<IIdentifierGenerator<InputId>>();
        _scopes = new AgentRunScopeFactory(services.GetRequiredService<IServiceScopeFactory>());
        ComponentRegistrations = validatedComposition.ComponentRegistrations;
        _pinnedRunProfiles = validatedComposition.RunProfiles.Publications.ToImmutableDictionary(
            static publication => (
                publication.SecurityProfile.AgentId,
                publication.SecurityProfile.AgentDefinitionRevision));
        _logger = services.GetService<ILogger<AgentEngine>>() ?? NullLogger<AgentEngine>.Instance;
    }

    /// <summary>Gets the composed service provider for the application that built the engine.</summary>
    /// <remarks>
    /// Runtime components never receive this provider through their constructors. A standalone engine disposes it
    /// with the runtime; a host-managed engine leaves disposal to the host.
    /// </remarks>
    internal IServiceProvider Services { get; }

    /// <summary>Gets the engine-wide clock captured at composition time.</summary>
    internal TimeProvider TimeProvider { get; }

    /// <summary>Gets the exact partial component-registration evidence validated for this engine.</summary>
    internal ComponentRegistrationSnapshot ComponentRegistrations { get; }

    /// <summary>Lists every agent this engine currently hosts.</summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The current catalog snapshot.</returns>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal async ValueTask<AgentCatalogSnapshot> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resolves one hosted agent to a runnable handle bound to this runtime.</summary>
    /// <param name="agentId">The agent identity to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>A resolved handle, or a typed not-found or invalid result.</returns>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal async ValueTask<AgentResolution> GetAgentAsync(AgentId agentId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var resolution = await _catalog.ResolveAsync(agentId, cancellationToken).ConfigureAwait(false);
        return resolution switch
        {
            ResolvedAgentDefinition resolved => new ResolvedAgent(new Agent(this, resolved.Definition, resolved.CatalogVersion)),
            AgentDefinitionNotFound notFound => new AgentNotFound(notFound.AgentId),
            InvalidAgentDefinition invalid => new InvalidAgent(invalid.AgentId, invalid.Diagnostics),
            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(AgentDefinitionResolution)} kind '{resolution.GetType()}'."),
        };
    }

    /// <summary>Creates a session without admitting a turn. Failure mapping matches the facade contract exactly.</summary>
    /// <param name="request">The agent, identity, conversation, idempotency key, and extensions.</param>
    /// <param name="cancellationToken">A token that cancels creation.</param>
    /// <returns>The created session, or closed safe failure evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    internal async Task<AgentSessionCreationResult> CreateSessionAsync(
        AgentSessionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var resolution = await _catalog.ResolveAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (resolution is not ResolvedAgentDefinition { Definition: var definition, CatalogVersion: var catalogVersion })
        {
            var reason = resolution switch
            {
                AgentDefinitionNotFound => "The requested agent is not hosted by this engine.",
                InvalidAgentDefinition invalid => $"The requested agent's definition is unusable: {string.Join("; ", invalid.Diagnostics)}",
                _ => $"Unrecognized {nameof(AgentDefinitionResolution)} kind '{resolution.GetType()}'.",
            };
            return new AgentSessionCreationFailed(
                request.AgentId, new SessionCreationFailure(SessionCreationFailureKind.AgentUnavailable, reason));
        }

        if (!_pinnedRunProfiles.TryGetValue((definition.Id, definition.Revision), out var pinnedPublication))
        {
            return new AgentSessionCreationFailed(
                request.AgentId,
                new SessionCreationFailure(
                    SessionCreationFailureKind.AgentUnavailable,
                    "The built composition has no pinned run-profile publication for this definition."));
        }

        await using var scope = Services.CreateAsyncScope();
        var loopKey = definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey;
        var sessions = DefaultAgentRunPlanCompiler.ResolveKeyedOrShared<ISessionCoordinator>(scope.ServiceProvider, loopKey.Value);

        SecurityAuthorizationContext authorization;
        try
        {
            authorization = await DefaultAgentRunPlanCompiler.CaptureAsync(
                _securityProfiles, _operationIds, definition, catalogVersion, pinnedPublication.SecurityProfile,
                sessionId: null, request.Identity, cancellationToken).ConfigureAwait(false);
        }
        catch (AgentAdmissionRejectedException exception)
        {
            return new AgentSessionCreationFailed(
                request.AgentId,
                new SessionCreationFailure(SessionCreationFailureKind.AuthorizationUnavailable, exception.Rejection.Reason));
        }

        var result = await sessions.CreateAsync(
            new SessionCreateRequest(definition.Id, request.Identity, authorization, request.ConversationId, request.IdempotencyKey, request.Extensions),
            pinnedPublication.SessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (result is not SessionCreated created)
        {
            return new AgentSessionCreationFailed(
                request.AgentId,
                new SessionCreationFailure(
                    SessionCreationFailureKind.StoreRejected, $"The session could not be created: {result.GetType().Name}."));
        }

        AgentAdmissionLog.SessionCreated(_logger, definition.Id, created.Descriptor.Address.SessionId);
        return new AgentSessionCreated(
            definition.Id, created.Descriptor.Address.SessionId, created.Descriptor.ConversationId, created.Existing);
    }

    /// <summary>Opens or validates a conversation binding without admitting a turn.</summary>
    /// <param name="request">The agent, identity, and optional session to bind.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>An opened binding or a safe rejection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or its identity is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="request.AgentId"/> is default.</exception>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    internal async Task<AgentConversationOpenResult> OpenSessionAsync(
        AgentConversationOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        ArgumentOutOfRangeException.ThrowIfEqual(request.AgentId, default);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var resolution = await GetAgentAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        if (resolution is not ResolvedAgent resolved)
        {
            return new AgentConversationOpenRejected(
                request.AgentId,
                resolution is InvalidAgent invalid
                    ? $"The requested agent's definition is unusable: {string.Join("; ", invalid.Diagnostics.Select(static diagnostic => diagnostic.SafeMessage))}"
                    : "The requested agent is not hosted by this engine.");
        }

        if (Services.GetService<IConversationEngineHost>() is { } host)
        {
            return await host.OpenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (request.SessionId is not { } sessionId)
        {
            return new AgentConversationOpenRejected(
                request.AgentId,
                "No conversation host is composed for this engine and no session was named to open.");
        }

        try
        {
            var definition = resolved.Agent.Definition;
            var (catalogVersion, publication) = await ValidatePinnedDefinitionAsync(definition, activity: null, cancellationToken)
                .ConfigureAwait(false);
            await using var scope = Services.CreateAsyncScope();
            var loopKey = definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey;
            var sessions = DefaultAgentRunPlanCompiler.ResolveKeyedOrShared<ISessionCoordinator>(scope.ServiceProvider, loopKey.Value);
            var descriptor = await OpenSessionAsync(
                definition,
                catalogVersion,
                publication.SecurityProfile,
                publication.SessionProfile,
                sessions,
                sessionId,
                request.Identity,
                cancellationToken).ConfigureAwait(false);
            return new AgentConversationOpened(definition.Id, descriptor.Address.SessionId, descriptor.ActiveBranchId);
        }
        catch (AgentAdmissionRejectedException exception)
        {
            return new AgentConversationOpenRejected(request.AgentId, exception.Rejection.Reason);
        }
    }

    /// <summary>Releases the standalone provider owned by this runtime. Disposal is idempotent.</summary>
    /// <returns>The shared disposal operation.</returns>
    internal ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            _disposed = true;
            _disposeTask ??= DisposeOwnedProviderAsync(_ownedProvider);
            return new ValueTask(_disposeTask);
        }
    }

    /// <summary>
    /// Admits one conversational turn and returns the loop result, throwing on pre-acceptance failure.
    /// </summary>
    /// <param name="agent">The pinned handle.</param>
    /// <param name="request">The turn to admit.</param>
    /// <param name="cancellationToken">Cancels the wait. After the user message is committed, cancellation settles inside the loop.</param>
    /// <returns>The loop's terminal result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An override widens the definition.</exception>
    /// <exception cref="AgentAdmissionRejectedException">Admission failed before the loop, including a busy session under <see cref="SessionBusyBehavior.Reject"/>.</exception>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before acceptance.</exception>
    internal async Task<AgentLoopResult> SendAsync(Agent agent, AgentSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(request);
        var outcome = await ExecuteAsync<object>(
            agent.Definition,
            request.SessionId,
            conversationId: null,
            request.Identity,
            input: null,
            request.Parts,
            new AgentRunOptions(request.MaxTurns, request.AttemptTimeout),
            executionLaneId: null,
            request.Observer,
            typedRejection: false,
            streaming: false,
            overrideParameterName: "request",
            cancellationToken).ConfigureAwait(false);
        return outcome.LoopResult ?? throw new InvalidOperationException("SendAsync completed without a loop result.");
    }

    /// <summary>
    /// Admits steering or follow-up input into an existing session without starting a run.
    /// </summary>
    /// <param name="agent">The pinned handle whose definition and security publication are revalidated.</param>
    /// <param name="sessionId">The session that already owns the lane. This method does not create a session.</param>
    /// <param name="identity">The already-authenticated caller. A fresh authorization capture must match it.</param>
    /// <param name="input">The immutable input. Its delivery class selects steering versus follow-up.</param>
    /// <param name="executionLaneId">The lane to admit into, or <see langword="null"/> to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels the wait before the queue commits. It does not undo a committed admission.</param>
    /// <returns>The queue's durable admission result. Failure does not fabricate a run identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/>, <paramref name="identity"/>, or <paramref name="input"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sessionId"/> or a present <paramref name="executionLaneId"/> is default.</exception>
    /// <exception cref="AgentAdmissionRejectedException">
    /// The definition is unavailable, the session is not visible, authorization could not be captured, or no input
    /// coordinator is composed.
    /// </exception>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled before admission committed.</exception>
    /// <remarks>
    /// Each call opens a fresh scope, installs <see cref="RunScopeState.Session"/>, captures a new
    /// <see cref="BeforeRunOperationCorrelation"/>, and admits through <see cref="IInputCoordinator.AdmitAsync"/>.
    /// It does not take the in-process busy gate and does not append history. Promotion remains the loop's job.
    /// </remarks>
    internal async Task<InputAdmissionResult> AdmitInputAsync(
        Agent agent,
        SessionId sessionId,
        ExecutionIdentity identity,
        AgentInput input,
        ExecutionLaneId? executionLaneId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(input);
        if (executionLaneId is { } suppliedLane)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(suppliedLane, default, nameof(executionLaneId));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var definition = agent.Definition;
        var (catalogVersion, publication) = await ValidatePinnedDefinitionAsync(definition, activity: null, cancellationToken)
            .ConfigureAwait(false);
        if (Services.GetService<IIdentityValidationPolicy>() is { } inputAdmissionValidation)
        {
            var validation = await inputAdmissionValidation.ValidateAsync(identity, cancellationToken).ConfigureAwait(false);
            if (validation is IdentityValidationRejected rejected)
            {
                AgentAdmissionLog.IdentityRevalidationRejected(_logger, definition.Id, rejected.Failure.Kind.ToString());
                throw AdmissionRejected(definition, catalogVersion, rejected.Failure.SafeMessage);
            }

            if (validation is not IdentityValidationPassed)
            {
                throw AdmissionRejected(definition, catalogVersion, "Identity validation returned an unsupported result.");
            }
        }

        AgentRunScopeLease? lease = null;
        try
        {
            var (result, prepared) = await _scopes.PrepareAsync(
                new ResolvedAgentDefinition(definition, catalogVersion),
                async (provider, token) =>
                {
                    var loopKey = (definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey).Value;
                    var sessions = DefaultAgentRunPlanCompiler.ResolveKeyedOrShared<ISessionCoordinator>(provider, loopKey);
                    _ = await OpenSessionAsync(
                        definition, catalogVersion, publication.SecurityProfile, publication.SessionProfile,
                        sessions, sessionId, identity, token).ConfigureAwait(false);
                    return new AgentRunRequest(
                        definition.Id, sessionId, null, identity, input, executionLaneId: executionLaneId);
                },
                cancellationToken).ConfigureAwait(false);
            lease = prepared ?? throw AdmissionRejected(
                definition,
                catalogVersion,
                result is InvalidAgentRunPlan invalid
                    ? string.Join("; ", invalid.Diagnostics.Select(static diagnostic => diagnostic.SafeMessage))
                    : "The run plan could not be compiled.");
            if (lease.Plan.Services.Input is not { } coordinator)
            {
                throw AdmissionRejected(definition, catalogVersion, "No input coordinator is composed for this agent.");
            }

            if (lease.Plan.Authorization.Scope.Correlation is not BeforeRunOperationCorrelation)
            {
                throw AdmissionRejected(definition, catalogVersion, "Input admission did not capture a before-run operation correlation.");
            }

            var laneId = executionLaneId ?? new ExecutionLaneId(sessionId.Value);
            return await coordinator.AdmitAsync(
                new InputAdmissionRequest(
                    definition.Id, sessionId, laneId, identity, lease.Plan.Authorization.Scope.Correlation,
                    lease.Plan.Authorization, input),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Records a durable abort for one in-process active run.</summary>
    /// <param name="agent">The pinned handle whose definition is revalidated.</param>
    /// <param name="runId">The accepted run to abort.</param>
    /// <param name="identity">The already-authenticated caller.</param>
    /// <param name="cancellationToken">Cancels the wait before the abort commits.</param>
    /// <returns>The session coordinator's typed abort outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> or <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default.</exception>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    /// <exception cref="AgentAdmissionRejectedException">The run is unknown to this process or the session is not visible.</exception>
    internal async Task<SessionRunAbortResult> CancelAsync(
        Agent agent,
        RunId runId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_activeRuns.TryGet(runId, out var registration) || registration.AgentId != agent.Id)
        {
            throw AdmissionRejected(agent.Definition, agent.CatalogVersion, "The requested run is not active in this process.");
        }

        var (_, publication) = await ValidatePinnedDefinitionAsync(agent.Definition, activity: null, cancellationToken)
            .ConfigureAwait(false);
        _ = await OpenSessionAsync(
            agent.Definition, agent.CatalogVersion, publication.SecurityProfile, registration.SessionProfile,
            registration.Sessions, registration.SessionId, identity, cancellationToken).ConfigureAwait(false);

        var loaded = await registration.Sessions.LoadRunStateAsync(
            new SessionRunStateRequest(registration.OperationContext), registration.Capability, cancellationToken).ConfigureAwait(false);
        if (loaded is not SessionRunStateLoaded runState)
        {
            throw AdmissionRejected(agent.Definition, agent.CatalogVersion, "The requested run state is unavailable.");
        }

        var sessionVersion = await CurrentSessionVersionAsync(
            registration.Sessions, registration.OperationContext, registration.BranchId, registration.SessionProfile, cancellationToken).ConfigureAwait(false)
            ?? throw AdmissionRejected(agent.Definition, agent.CatalogVersion, "The session version could not be read.");
        var abort = new SessionRunAbortRequest(
            registration.OperationContext,
            runState.State.OperationStateRevision,
            runState.State.LaneRevision,
            sessionVersion,
            new IdempotencyKey($"agentkit.engine:{runId}:abort"));
        return await registration.Sessions.AbortRunAsync(abort, registration.Capability, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Attaches to one in-process active run's replay and live event tail.</summary>
    /// <typeparam name="TOutput">The validated output snapshot type.</typeparam>
    /// <param name="agent">The pinned handle whose definition is revalidated.</param>
    /// <param name="runId">The accepted run to attach to.</param>
    /// <param name="sessionId">The session the run belongs to; used for typed rejection evidence when attach is unavailable.</param>
    /// <param name="identity">The already-authenticated caller.</param>
    /// <param name="cancellationToken">Cancels attachment setup. It does not abort the run.</param>
    /// <returns>A started attach stream, or a rejection when the run settled or cannot be tailed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> or <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> or <paramref name="sessionId"/> is default.</exception>
    /// <exception cref="ObjectDisposedException">This runtime has been disposed.</exception>
    internal async Task<AgentRunStreamStartResult<TOutput>> AttachAsync<TOutput>(
        Agent agent,
        RunId runId,
        SessionId sessionId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_activeRuns.TryGet(runId, out var registration) || registration.AgentId != agent.Id)
        {
            return new AgentRunStreamRejected<TOutput>(Reject<TOutput>(
                agent.Id,
                sessionId,
                AgentErrorCodes.InvalidState,
                "The requested run is not active in this process."));
        }

        if (registration.Publisher is not { } publisher)
        {
            return new AgentRunStreamRejected<TOutput>(Reject<TOutput>(
                agent.Id,
                registration.SessionId,
                AgentErrorCodes.UnsupportedCapability,
                "The composed output publisher does not expose a live subscription."));
        }

        var (_, publication) = await ValidatePinnedDefinitionAsync(agent.Definition, activity: null, cancellationToken)
            .ConfigureAwait(false);
        _ = await OpenSessionAsync(
            agent.Definition, agent.CatalogVersion, publication.SecurityProfile, registration.SessionProfile,
            registration.Sessions, registration.SessionId, identity, cancellationToken).ConfigureAwait(false);

        var live = publisher.Subscribe<TOutput>();
        return new AgentRunStreamStarted<TOutput>(
            new AttachedAgentRunStream<TOutput>(live, registration, TimeProvider));
    }

    /// <summary>Runs one existing session and returns a typed finished or rejected envelope.</summary>
    /// <typeparam name="TOutput">The requested output type.</typeparam>
    /// <param name="agent">The pinned handle.</param>
    /// <param name="sessionId">The session to open. This method does not create a session.</param>
    /// <param name="conversationId">Ignored in favor of the session's stored conversation. Present only to match the caller signature.</param>
    /// <param name="identity">The already-authenticated identity.</param>
    /// <param name="input">The input to admit.</param>
    /// <param name="options">Narrowing overrides, or null.</param>
    /// <param name="executionLaneId">The lane to advance, or null to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A finished envelope, or a rejection that carries no run identity.</returns>
    internal Task<AgentRunResult<TOutput>> RunAsync<TOutput>(
        Agent agent,
        SessionId sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        AgentInput input,
        AgentRunOptions? options,
        ExecutionLaneId? executionLaneId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(input);
        return RunCoreAsync<TOutput>(agent.Definition, sessionId, conversationId, identity, input, options, executionLaneId, cancellationToken);
    }

    /// <summary>Resolves <paramref name="request"/>'s agent and runs it.</summary>
    /// <typeparam name="TOutput">The requested output type.</typeparam>
    /// <param name="request">The process-level run request.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A finished envelope, or a rejection that carries no run identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    internal async Task<AgentRunResult<TOutput>> RunAsync<TOutput>(AgentRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var resolution = await GetAgentAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        return resolution is not ResolvedAgent resolved
            ? Reject<TOutput>(
                request.AgentId,
                request.SessionId,
                resolution is InvalidAgent ? AgentErrorCodes.InvalidConfiguration : AgentErrorCodes.InvalidState,
                resolution is InvalidAgent invalid
                    ? $"The requested agent's definition is unusable: {string.Join("; ", invalid.Diagnostics)}"
                    : "The requested agent is not hosted by this engine.")
            : await RunCoreAsync<TOutput>(
            resolved.Agent.Definition,
            request.SessionId,
            request.ConversationId,
            request.Identity,
            request.Input,
            request.Options,
            request.ExecutionLaneId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Subscribes to one run before driving it.</summary>
    /// <typeparam name="TOutput">The requested output type.</typeparam>
    /// <param name="request">The process-level run request.</param>
    /// <param name="cancellationToken">Cancels admission and the drive. Disposing the returned stream does not.</param>
    /// <returns>A started stream, or a rejection with no subscription and no run identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    internal async Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var resolution = await GetAgentAsync(request.AgentId, cancellationToken).ConfigureAwait(false);
        return resolution is not ResolvedAgent resolved
            ? new AgentRunStreamRejected<TOutput>(Reject<TOutput>(
                request.AgentId,
                request.SessionId,
                resolution is InvalidAgent ? AgentErrorCodes.InvalidConfiguration : AgentErrorCodes.InvalidState,
                resolution is InvalidAgent invalid
                    ? $"The requested agent's definition is unusable: {string.Join("; ", invalid.Diagnostics)}"
                    : "The requested agent is not hosted by this engine."))
            : await StreamCoreAsync<TOutput>(
            resolved.Agent.Definition, request.SessionId, request.ConversationId, request.Identity, request.Input,
            request.Options, request.ExecutionLaneId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Subscribes to one handle's run before driving it.</summary>
    /// <typeparam name="TOutput">The requested output type.</typeparam>
    /// <param name="agent">The pinned handle.</param>
    /// <param name="sessionId">The existing session.</param>
    /// <param name="conversationId">Accepted for signature compatibility; the session's conversation is used.</param>
    /// <param name="identity">The already-authenticated identity.</param>
    /// <param name="input">The input to admit.</param>
    /// <param name="options">Narrowing overrides, or null.</param>
    /// <param name="executionLaneId">The lane to advance, or null to derive one from the session.</param>
    /// <param name="cancellationToken">Cancels admission and the drive.</param>
    /// <returns>A started stream, or a rejection with no subscription and no run identity.</returns>
    internal Task<AgentRunStreamStartResult<TOutput>> StreamAsync<TOutput>(
        Agent agent,
        SessionId sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        AgentInput input,
        AgentRunOptions? options,
        ExecutionLaneId? executionLaneId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(input);
        return StreamCoreAsync<TOutput>(agent.Definition, sessionId, conversationId, identity, input, options, executionLaneId, cancellationToken);
    }

    private async Task<AgentRunResult<TOutput>> RunCoreAsync<TOutput>(
        AgentDefinition definition,
        SessionId sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        AgentInput input,
        AgentRunOptions? options,
        ExecutionLaneId? executionLaneId,
        CancellationToken cancellationToken)
    {
        var outcome = await ExecuteAsync<TOutput>(
            definition, sessionId, conversationId, identity, input, parts: default, options, executionLaneId,
            observer: null, typedRejection: true, streaming: false, overrideParameterName: "options", cancellationToken).ConfigureAwait(false);
        return outcome.Rejection ?? (AgentRunResult<TOutput>) outcome.Finished!;
    }

    private async Task<AgentRunStreamStartResult<TOutput>> StreamCoreAsync<TOutput>(
        AgentDefinition definition,
        SessionId sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        AgentInput input,
        AgentRunOptions? options,
        ExecutionLaneId? executionLaneId,
        CancellationToken cancellationToken)
    {
        var outcome = await ExecuteAsync<TOutput>(
            definition, sessionId, conversationId, identity, input, parts: default, options, executionLaneId,
            observer: null, typedRejection: true, streaming: true, overrideParameterName: "options", cancellationToken).ConfigureAwait(false);
        return outcome.Rejection is { } rejection
            ? new AgentRunStreamRejected<TOutput>(rejection)
            : new AgentRunStreamStarted<TOutput>(outcome.Stream!);
    }

    private async Task<ExecutionOutcome<TOutput>> ExecuteAsync<TOutput>(
        AgentDefinition definition,
        SessionId? requestedSessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        AgentInput? input,
        ImmutableArray<ContentPart> parts,
        AgentRunOptions? options,
        ExecutionLaneId? executionLaneId,
        IAgentRunObserver? observer,
        bool typedRejection,
        bool streaming,
        string overrideParameterName,
        CancellationToken cancellationToken)
    {
        _ = conversationId;
        ObjectDisposedException.ThrowIf(_disposed, this);
        var activity = AgentAdmissionObservability.Start(definition.Id);
        var admissionCompleted = false;
        AgentRunScopeLease? lease = null;
        InProcessSessionGate.Lease? gate = null;
        var handedOff = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (catalogVersion, pinnedPublication) = await ValidatePinnedDefinitionAsync(definition, activity, cancellationToken)
                .ConfigureAwait(false);
            if (await RevalidateIdentityAtAdmissionAsync<TOutput>(
                    definition,
                    identity,
                    requestedSessionId ?? default,
                    catalogVersion,
                    activity,
                    typedRejection,
                    cancellationToken).ConfigureAwait(false) is { } identityRejection)
            {
                admissionCompleted = true;
                return identityRejection;
            }

            if (!IsOutputCompatible<TOutput>(definition.Output))
            {
                return FailBeforeAcceptance<TOutput>(
                    definition, requestedSessionId ?? default, catalogVersion, activity, ref admissionCompleted, typedRejection,
                    AgentErrorCodes.IncompatibleSchema,
                    "The requested output type is incompatible with the pinned output definition.");
            }

            var maxTurns = ResolveMaxTurns(definition, options?.MaxTurns, overrideParameterName);
            var attemptTimeout = ResolveAttemptTimeout(definition, options?.AttemptTimeout, overrideParameterName);
            var security = pinnedPublication.SecurityProfile;
            var sessionProfile = pinnedPublication.SessionProfile;
            SessionDescriptor? opened = null;
            AgentInput? capturedInput = null;
            var (Result, Lease) = await _scopes.PrepareAsync(
                new ResolvedAgentDefinition(definition, catalogVersion),
                async (provider, token) =>
                {
                    var loopKey = (definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey).Value;
                    var sessions = DefaultAgentRunPlanCompiler.ResolveKeyedOrShared<ISessionCoordinator>(provider, loopKey);
                    opened = requestedSessionId is { } sessionId
                        ? await OpenSessionAsync(definition, catalogVersion, security, sessionProfile, sessions, sessionId, identity, token).ConfigureAwait(false)
                        : await CreateSessionAsync(definition, catalogVersion, security, sessionProfile, sessions, identity, token).ConfigureAwait(false);
                    capturedInput = input ?? new AgentInput(
                        _inputIds.Create(), InputDelivery.Steer, parts, ExtensionData.Empty);
                    return new AgentRunRequest(
                        definition.Id,
                        opened.Address.SessionId,
                        opened.ConversationId,
                        identity,
                        capturedInput,
                        options,
                        executionLaneId);
                },
                cancellationToken).ConfigureAwait(false);
            if (Lease is null)
            {
                var reason = Result is InvalidAgentRunPlan invalid
                    ? string.Join("; ", invalid.Diagnostics.Select(static diagnostic => diagnostic.SafeMessage))
                    : "The run plan could not be compiled.";
                return FailBeforeAcceptance<TOutput>(
                    definition, opened?.Address.SessionId ?? requestedSessionId ?? default, catalogVersion, activity,
                    ref admissionCompleted, typedRejection, AgentErrorCodes.InvalidConfiguration, reason);
            }

            lease = Lease;
            var plan = lease.Plan;
            var descriptor = opened ?? throw new InvalidOperationException("Session admission did not capture a descriptor.");
            var sessionId = descriptor.Address.SessionId;
            var branchId = descriptor.ActiveBranchId;
            _ = activity?.SetTag(AgentKitTagNames.SessionId, sessionId.ToString());

            var laneId = executionLaneId ?? new ExecutionLaneId(sessionId.Value);
            var acquisition = await _gate.TryEnterAsync(definition.Id, sessionId, sessionProfile.BusyBehavior, cancellationToken)
                .ConfigureAwait(false);
            if (!acquisition.Acquired)
            {
                AgentAdmissionObservability.Complete(activity, _logger, "busy", AgentErrorCodes.SessionBusy.ToString());
                activity = null;
                admissionCompleted = true;
                var busy = "The session is busy with another run; the session profile rejects concurrent turns.";
                return !typedRejection
                    ? throw AdmissionRejected(definition, catalogVersion, busy)
                    : new ExecutionOutcome<TOutput>(Reject<TOutput>(
                    definition.Id, sessionId, AgentErrorCodes.SessionBusy, busy, retryable: true));
            }

            gate = acquisition.Held
                ?? throw new InvalidOperationException("The in-process gate did not return a lease.");
            var sessionsForRun = plan.Session.Coordinator;
            var capability = plan.Session;
            var beforeRunCorrelation = plan.Authorization.Scope.Correlation;
            var beforeRunContext = new SessionOperationContext(
                definition.Id, sessionId, laneId, beforeRunCorrelation, identity, plan.Authorization);
            var tip = await LoadBranchTipAsync(definition, catalogVersion, sessionsForRun, capability.Profile, beforeRunContext, branchId, cancellationToken)
                .ConfigureAwait(false);
            var laneState = await sessionsForRun.LoadLaneStateAsync(new SessionLaneStateRequest(beforeRunContext), capability, cancellationToken)
                .ConfigureAwait(false);

            SessionLaneRevision laneRevision;
            SessionBranchCursor branchCursor;
            SessionVersion sessionVersion;
            switch (laneState)
            {
                case SessionLaneStateLoaded { State.AcceptedState: not null }:
                    throw AdmissionRejected(definition, catalogVersion, "The session's execution lane is occupied by an unreleased prior run.");
                case SessionLaneStateLoaded loaded:
                    laneRevision = loaded.State.Revision;
                    branchCursor = loaded.State.BranchCursor;
                    sessionVersion = tip.Version;
                    break;
                case SessionLaneStateNotProvisioned:
                    var provisionResult = await sessionsForRun.ProvisionLaneAsync(
                        new SessionExecutionLaneProvisionRequest(
                            beforeRunContext, tip.Cursor, tip.Version, _entryIds.Create(), capability.Profile.Reference,
                            new RunConfigurationReference(
                                plan.Authorization.ConfigurationVersion,
                                RunPolicyVersioning.Compute(maxTurns, attemptTimeout, AgentLoopComponentDefaults.ContinuationPolicyKey),
                                capability.Profile.ConfigurationFingerprint),
                            TimeProvider.GetUtcNow(),
                            new IdempotencyKey($"agentkit.engine:{sessionId}:lane:{laneId}")),
                        capability, cancellationToken).ConfigureAwait(false);
                    if (provisionResult is not SessionExecutionLaneProvisioned provisioned)
                    {
                        throw AdmissionRejected(
                            definition, catalogVersion, $"The session's execution lane could not be provisioned: {provisionResult.GetType().Name}.");
                    }

                    laneRevision = provisioned.LaneRevision;
                    branchCursor = provisioned.BranchCursor;
                    sessionVersion = provisioned.SessionVersion;
                    break;
                default:
                    throw AdmissionRejected(definition, catalogVersion, "The session's execution lane could not be discovered.");
            }

            var runId = _runIds.Create();
            gate.AssignRun(runId);
            lease.BindRunIdentity(new RunScopeIdentity(definition.Id, sessionId, descriptor.ConversationId, runId));
            var runServices = ResolveRunScopedServices(lease, definition, plan.Services);
            if (streaming && runServices.Publisher is not ISubscribableOutputPublisher)
            {
                return FailBeforeAcceptance<TOutput>(
                    definition, sessionId, catalogVersion, activity, ref admissionCompleted, typedRejection: true,
                    AgentErrorCodes.UnsupportedCapability,
                    "The composed output publisher does not expose a live subscription.");
            }

            var effectiveInput = capturedInput ?? throw new InvalidOperationException("Admission did not capture input.");

            var now = TimeProvider.GetUtcNow();
            var policyVersion = RunPolicyVersioning.Compute(maxTurns, attemptTimeout, AgentLoopComponentDefaults.ContinuationPolicyKey);
            var configuration = new RunConfigurationReference(
                plan.Authorization.ConfigurationVersion, policyVersion, capability.Profile.ConfigurationFingerprint);
            var admissionId = _admissionIds.Create();
            var admissionEntryId = _entryIds.Create();
            var fingerprint = InputPayloadFingerprint.Create(effectiveInput);
            var preprocessing = new InputPreprocessingManifest(new ConfigurationVersion(1), fingerprint, fingerprint);
            var admissionResult = await sessionsForRun.AdmitInputAsync(
                new SessionInputAdmissionRequest(
                    beforeRunContext, admissionId, admissionEntryId, effectiveInput, effectiveInput, preprocessing, now, sessionVersion,
                    laneRevision, branchCursor, new IdempotencyKey($"agentkit.engine:{runId}:admit"), maximumPendingInputs: 8),
                capability, cancellationToken).ConfigureAwait(false);
            if (admissionResult is not AcceptedInput accepted)
            {
                throw AdmissionRejected(definition, catalogVersion, $"The user input could not be admitted: {admissionResult.GetType().Name}.");
            }

            var initialTurnId = _turnIds.Create();
            var acceptedCorrelation = new InRunOperationCorrelation(
                beforeRunCorrelation.OperationId,
                runId,
                initialTurnId);
            var acceptedAuthorization = await DefaultAgentRunPlanCompiler.CaptureAsync(
                _securityProfiles, definition, catalogVersion, security, sessionId, acceptedCorrelation, identity, cancellationToken)
                .ConfigureAwait(false);
            var messageId = _messageIds.Create();
            var admittedCursor = new SessionBranchCursor(branchId, admissionEntryId);
            var startResult = await sessionsForRun.AcceptRunAsync(
                new SessionRunStartRequest(
                    beforeRunContext, admissionId, [admissionId], accepted.Receipt.AdmittedSequence,
                    new SessionLaneRevision(laneRevision.Value + 1), new SessionVersion(sessionVersion.Value + 1),
                    admittedCursor, expectedFencingToken: null, runId, initialTurnId, _entryIds.Create(),
                    [_entryIds.Create()], [messageId], _entryIds.Create(), new OperationStateRevision(1),
                    capability.Profile.Reference, configuration, acceptedAuthorization, now,
                    new IdempotencyKey($"agentkit.engine:{runId}:accept")),
                capability, cancellationToken).ConfigureAwait(false);
            if (startResult is not SessionRunAccepted)
            {
                throw AdmissionRejected(definition, catalogVersion, $"The run could not be accepted: {startResult.GetType().Name}.");
            }

            var previousCursor = new MessageCursor(
                definition.Id, sessionId, descriptor.ConversationId, branchId, tip.Version, tip.Sequence);
            var inRunContext = new SessionOperationContext(
                definition.Id, sessionId, laneId, acceptedCorrelation, identity, acceptedAuthorization);
            _activeRuns.Register(new ActiveRunRegistration(
                definition.Id,
                sessionId,
                descriptor.ConversationId,
                branchId,
                runId,
                capability,
                inRunContext,
                previousCursor,
                capability.Profile,
                sessionsForRun,
                runServices.Publisher as ISubscribableOutputPublisher));

            var laneAdmission = new LoopLaneAdmission(laneId, acceptedCorrelation, new OperationStateRevision(1));
            var runRequest = BuildRunRequest(
                definition, pinnedPublication, sessionId, branchId, runId, identity, acceptedAuthorization, maxTurns, attemptTimeout) with
            {
                Observer = observer,
                LaneAdmission = laneAdmission,
            };
            AgentAdmissionObservability.Complete(activity, _logger, "admitted");
            activity = null;
            admissionCompleted = true;

            if (streaming)
            {
                var subscribable = (ISubscribableOutputPublisher) runServices.Publisher!;
                var stream = subscribable.Subscribe<TOutput>();
                var capturedLease = lease;
                var capturedGate = gate;
                lease = null;
                gate = null;
                handedOff = true;
                _ = FinishStreamAsync<TOutput>(
                    capturedLease, capturedGate, plan.Loop, runRequest, runServices, sessionsForRun, capability,
                    definition.Id, sessionId, laneId, acceptedCorrelation, identity, acceptedAuthorization, runId,
                    descriptor.ConversationId, previousCursor, runServices.Publisher, cancellationToken);
                return new ExecutionOutcome<TOutput>(stream);
            }

            try
            {
                var loopResult = await plan.Loop.RunAsync(runRequest, runServices, cancellationToken).ConfigureAwait(false);
                if (loopResult.FinalVersion is { } finalVersion)
                {
                    await ReleaseLaneAsync(
                        sessionsForRun, capability, definition.Id, sessionId, laneId, acceptedCorrelation,
                        identity, acceptedAuthorization, finalVersion, runId).ConfigureAwait(false);
                }

                return !typedRejection
                    ? new ExecutionOutcome<TOutput>(loopResult)
                    : new ExecutionOutcome<TOutput>(Finish<TOutput>(loopResult, descriptor.ConversationId, previousCursor));
            }
            finally
            {
                _activeRuns.Unregister(runId);
            }
        }
        catch (OperationCanceledException) when (!admissionCompleted && cancellationToken.IsCancellationRequested)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "cancelled", nameof(OperationCanceledException));
            AgentAdmissionObservability.LogCancelled(_logger);
            throw;
        }
        catch (AgentAdmissionRejectedException exception) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "rejected", nameof(AgentAdmissionRejectedException));
            activity = null;
            admissionCompleted = true;
            if (!typedRejection)
            {
                throw;
            }

            var sessionId = requestedSessionId ?? default;
            if (sessionId == default)
            {
                throw;
            }

            return new ExecutionOutcome<TOutput>(Reject<TOutput>(
                exception.Rejection.AgentId, sessionId, Classify(exception.Rejection.Reason), exception.Rejection.Reason));
        }
        catch (Exception exception) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "failed", exception.GetType().Name);
            AgentAdmissionObservability.LogFailed(_logger, exception.GetType().Name);
            throw;
        }
        finally
        {
            if (!handedOff)
            {
                gate?.Dispose();
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask<ExecutionOutcome<TOutput>?> RevalidateIdentityAtAdmissionAsync<TOutput>(
        AgentDefinition definition,
        ExecutionIdentity identity,
        SessionId sessionId,
        AgentCatalogVersion catalogVersion,
        Activity? activity,
        bool typedRejection,
        CancellationToken cancellationToken)
    {
        if (Services.GetService<IIdentityValidationPolicy>() is not { } policy)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var validation = await policy.ValidateAsync(identity, cancellationToken).ConfigureAwait(false);
        if (validation is IdentityValidationPassed)
        {
            return null;
        }

        var failure = validation is IdentityValidationRejected rejected
            ? rejected.Failure
            : new IdentityFailure(
                IdentityFailureKind.Unavailable,
                "Identity validation returned an unsupported result.",
                identity.Evidence.Issuer);
        try
        {
            AgentAdmissionLog.IdentityRevalidationRejected(_logger, definition.Id, failure.Kind.ToString());
        }
        catch (Exception)
        {
        }

        var admissionCompleted = false;
        return FailBeforeAcceptance<TOutput>(
            definition,
            sessionId,
            catalogVersion,
            activity,
            ref admissionCompleted,
            typedRejection,
            AgentErrorCodes.AuthenticationFailed,
            failure.SafeMessage);
    }

    private ExecutionOutcome<TOutput> FailBeforeAcceptance<TOutput>(
        AgentDefinition definition,
        SessionId sessionId,
        AgentCatalogVersion catalogVersion,
        Activity? activity,
        ref bool admissionCompleted,
        bool typedRejection,
        AgentErrorCode code,
        string reason)
    {
        AgentAdmissionObservability.Complete(activity, _logger, "rejected", nameof(AgentAdmissionRejectedException));
        admissionCompleted = true;
        return !typedRejection || sessionId == default
            ? throw AdmissionRejected(definition, catalogVersion, reason)
            : new ExecutionOutcome<TOutput>(Reject<TOutput>(definition.Id, sessionId, code, reason));
    }

    private async Task FinishStreamAsync<TOutput>(
        AgentRunScopeLease lease,
        InProcessSessionGate.Lease gate,
        IAgentLoop loop,
        AgentLoopRunRequest request,
        AgentRunServices services,
        ISessionCoordinator sessions,
        SessionExecutionCapability capability,
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId laneId,
        InRunOperationCorrelation correlation,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        RunId runId,
        ConversationId? conversationId,
        MessageCursor previousCursor,
        IOutputPublisher? publisher,
        CancellationToken cancellationToken)
    {
        try
        {
            var loopResult = await loop.RunAsync(request, services, cancellationToken).ConfigureAwait(false);
            var finished = Finish<TOutput>(loopResult, conversationId, previousCursor);
            if (publisher is not null)
            {
                await publisher.CompleteAsync(finished, CancellationToken.None).ConfigureAwait(false);
            }

            if (loopResult.FinalVersion is { } finalVersion)
            {
                await ReleaseLaneAsync(
                    sessions, capability, agentId, sessionId, laneId, correlation, identity, authorization, finalVersion, runId)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _activeRuns.Unregister(runId);
            gate.Dispose();
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<(AgentCatalogVersion CatalogVersion, AgentRunProfilePublication Publication)> ValidatePinnedDefinitionAsync(
        AgentDefinition definition,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var snapshot = await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        _ = activity?.SetTag(AgentKitTagNames.AgentCatalogVersion, snapshot.Version.ToString());
        var current = snapshot.FindDefinition(definition.Id);
        if (current is null || current.Revision != definition.Revision || !current.Equals(definition))
        {
            throw AdmissionRejected(
                definition, snapshot.Version,
                current is null
                    ? "The pinned agent definition is no longer enabled for new admission."
                    : "The pinned agent definition was replaced and cannot be silently upgraded.");
        }

        if (!_pinnedRunProfiles.TryGetValue((definition.Id, definition.Revision), out var pinnedPublication))
        {
            throw AdmissionRejected(definition, snapshot.Version, "The built composition has no pinned run-profile publication for this definition.");
        }

        var publicationResult = await _runProfiles.ReadAsync(definition.Id, definition.Revision, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return publicationResult is AgentRunProfilePublicationFound found && found.Publication == pinnedPublication
            ? (snapshot.Version, pinnedPublication)
            : throw AdmissionRejected(definition, snapshot.Version, "The exact run-profile publication changed after composition validation.");
    }

    private static async Task<SessionVersion?> CurrentSessionVersionAsync(
        ISessionCoordinator sessions,
        SessionOperationContext context,
        BranchId branchId,
        SessionProfileSnapshot profile,
        CancellationToken cancellationToken)
    {
        var page = await sessions.ReadAsync(
            new SessionReadRequest(context, branchId, new SessionSequence(0), pageSize: 1),
            profile,
            cancellationToken).ConfigureAwait(false);
        return page is SessionPage { Snapshot: { } snapshot } ? snapshot.Version : null;
    }

    private static async Task<(SessionVersion Version, SessionBranchCursor Cursor, SessionSequence Sequence)> LoadBranchTipAsync(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        ISessionCoordinator sessions,
        SessionProfileSnapshot sessionProfile,
        SessionOperationContext context,
        BranchId branchId,
        CancellationToken cancellationToken)
    {
        var head = await sessions.ReadAsync(
            new SessionReadRequest(context, branchId, new SessionSequence(0), pageSize: 1), sessionProfile, cancellationToken)
            .ConfigureAwait(false);
        if (head is not SessionPage { Snapshot: { } snapshot })
        {
            throw AdmissionRejected(definition, catalogVersion, "The session's current history snapshot could not be captured.");
        }

        if (snapshot.UpperSequence.Value == 0)
        {
            return (snapshot.Version, new SessionBranchCursor(branchId, null), snapshot.UpperSequence);
        }

        var tail = await sessions.ReadAsync(
            new SessionReadRequest(
                context, branchId, new SessionSequence(snapshot.UpperSequence.Value - 1), pageSize: 1, snapshot),
            sessionProfile, cancellationToken).ConfigureAwait(false);
        return tail is SessionPage { Entries.Length: > 0 } page
            ? (snapshot.Version, new SessionBranchCursor(branchId, page.Entries[^1].Id), snapshot.UpperSequence)
            : throw AdmissionRejected(definition, catalogVersion, "The session's current branch tip could not be captured.");
    }

    private async Task ReleaseLaneAsync(
        ISessionCoordinator sessions,
        SessionExecutionCapability capability,
        AgentId agentId,
        SessionId sessionId,
        ExecutionLaneId executionLaneId,
        InRunOperationCorrelation acceptedCorrelation,
        ExecutionIdentity identity,
        SecurityAuthorizationContext acceptedAuthorization,
        SessionVersion finalVersion,
        RunId runId)
    {
        try
        {
            var releaseContext = new SessionOperationContext(
                agentId, sessionId, executionLaneId, acceptedCorrelation, identity, acceptedAuthorization);
            var release = new SessionRunReleaseRequest(
                releaseContext, new OperationStateRevision(1), finalVersion,
                new IdempotencyKey($"agentkit.engine:{runId}:release"));
            var result = await sessions.ReleaseRunAsync(release, capability, CancellationToken.None).ConfigureAwait(false);
            if (result is not SessionRunReleased)
            {
                AgentAdmissionLog.LaneReleaseRejected(_logger, agentId, sessionId, result.GetType().Name);
            }
        }
        catch (Exception exception)
        {
            AgentAdmissionLog.LaneReleaseFaulted(_logger, agentId, sessionId, exception.GetType().FullName ?? exception.GetType().Name);
        }
    }

    private async Task<SessionDescriptor> CreateSessionAsync(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        SecurityProfilePublication security,
        SessionProfileSnapshot sessionProfile,
        ISessionCoordinator sessions,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        var authorization = await DefaultAgentRunPlanCompiler.CaptureAsync(
            _securityProfiles, _operationIds, definition, catalogVersion, security, null, identity, cancellationToken)
            .ConfigureAwait(false);
        var correlation = authorization.Scope.Correlation;
        var result = await sessions.CreateAsync(
            new SessionCreateRequest(
                definition.Id, identity, authorization, null,
                new IdempotencyKey($"agentkit.engine:{definition.Id}:create:{correlation.OperationId}"), ExtensionData.Empty),
            sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (result is not SessionCreated created)
        {
            throw AdmissionRejected(definition, catalogVersion, $"The session could not be created: {result.GetType().Name}.");
        }

        AgentAdmissionLog.SessionCreated(_logger, definition.Id, created.Descriptor.Address.SessionId);
        return created.Descriptor;
    }

    private async Task<SessionDescriptor> OpenSessionAsync(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        SecurityProfilePublication security,
        SessionProfileSnapshot sessionProfile,
        ISessionCoordinator sessions,
        SessionId sessionId,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        var authorization = await DefaultAgentRunPlanCompiler.CaptureAsync(
            _securityProfiles, _operationIds, definition, catalogVersion, security, sessionId, identity, cancellationToken)
            .ConfigureAwait(false);
        var result = await sessions.LoadAsync(
            new SessionOperationContext(definition.Id, sessionId, null, authorization.Scope.Correlation, identity, authorization),
            sessionProfile,
            cancellationToken).ConfigureAwait(false);
        if (result is not SessionLoaded { Descriptor: var descriptor }
            || descriptor.Address.AgentId != definition.Id
            || descriptor.TenantId != identity.TenantId
            || descriptor.OwnerId != identity.PrincipalId
            || descriptor.State != SessionLifecycleState.Active)
        {
            AgentAdmissionLog.SessionNotVisible(_logger, definition.Id, sessionId);
            throw AdmissionRejected(definition, catalogVersion, "The requested session is unavailable or not visible to this agent and identity.");
        }

        return descriptor;
    }

    private static AgentLoopRunRequest BuildRunRequest(
        AgentDefinition definition,
        AgentRunProfilePublication pinnedPublication,
        SessionId sessionId,
        BranchId branchId,
        RunId runId,
        ExecutionIdentity identity,
        SecurityAuthorizationContext authorization,
        int maxTurns,
        TimeSpan attemptTimeout) =>
        pinnedPublication.Configuration is { } configuration
            ? new AgentLoopRunRequest(
                definition, sessionId, branchId, runId, identity, authorization, pinnedPublication.SessionProfile,
                configuration, maxTurns, attemptTimeout, definition.Extensions)
            : new AgentLoopRunRequest(
                definition.Id, sessionId, branchId, runId, identity, authorization, pinnedPublication.SessionProfile,
                definition.Models, definition.ModelRequirements, definition.Instructions, definition.Tools,
                definition.ToolChoice, definition.Settings, maxTurns, attemptTimeout, definition.Extensions)
            {
                Output = definition.Output,
                HookProfile = definition.HookProfile,
            };

    private static AgentAdmissionRejectedException AdmissionRejected(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        string reason) => new(new AgentAdmissionRejection(definition.Id, definition.Revision, catalogVersion, reason));

    private static int ResolveMaxTurns(AgentDefinition definition, int? maxTurns, string paramName) =>
        maxTurns is not { } requested
            ? definition.RunDefaults.MaxTurns
            : requested <= definition.RunDefaults.MaxTurns
                ? requested
                : throw new ArgumentOutOfRangeException(
                    paramName,
                    requested,
                    "A run override may only narrow the definition's turn limit of "
                    + $"{definition.RunDefaults.MaxTurns}.");

    private static TimeSpan ResolveAttemptTimeout(AgentDefinition definition, TimeSpan? attemptTimeout, string paramName) =>
        attemptTimeout is not { } requested
            ? definition.RunDefaults.AttemptTimeout
            : requested <= definition.RunDefaults.AttemptTimeout
                ? requested
                : throw new ArgumentOutOfRangeException(
                    paramName,
                    requested,
                    "A run override may only narrow the definition's attempt timeout of "
                    + $"{definition.RunDefaults.AttemptTimeout}.");

    private static bool IsOutputCompatible<TOutput>(OutputDefinition? output)
    {
        return typeof(TOutput) == typeof(string)
            || typeof(TOutput) == typeof(object)
            || typeof(TOutput) == typeof(ValidatedOutput)
            || (output?.RuntimeType is { } runtimeType && typeof(TOutput).IsAssignableFrom(runtimeType));
    }

    private static AgentRunFinished<TOutput> Finish<TOutput>(
        AgentLoopResult result,
        ConversationId? conversationId,
        MessageCursor previousCursor)
    {
        var deferred = result.Outcome is RunDeferred handoff
            ? handoff.Requests
            : [];
        return new AgentRunFinished<TOutput>(
            result.AgentId,
            result.SessionId,
            conversationId,
            result.RunId,
            result.Outcome,
            result.Settlement,
            ProjectOutput<TOutput>(result),
            previousCursor,
            result.NewMessages,
            result.Usage,
            deferred,
            ExtensionData.Empty);
    }

    private static TOutput? ProjectOutput<TOutput>(AgentLoopResult result)
    {
        return result.Output?.Value is TOutput value
            ? value
            : result.Output is TOutput output
                ? output
                : typeof(TOutput) == typeof(string)
                    ? (TOutput) (object) ProjectAssistantText(result.NewMessages)
                    : default;
    }

    private static string ProjectAssistantText(ImmutableArray<AgentMessage> messages)
    {
        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            if (message is not AssistantMessage)
            {
                continue;
            }

            foreach (var part in message.Parts)
            {
                if (part is TextPart text)
                {
                    _ = builder.Append(text.Text);
                }
            }
        }

        return builder.ToString();
    }

    private static AgentRunServices ResolveRunScopedServices(
        AgentRunScopeLease lease,
        AgentDefinition definition,
        AgentRunServices compiled)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(compiled);
        var provider = lease.ScopeProvider;
        var outputKey = (definition.OutputPublisherKey ?? AgentIOComponentDefaults.OutputPublisherKey).Value;
        var inputKey = (definition.InputCoordinatorKey ?? AgentIOComponentDefaults.InputCoordinatorKey).Value;
        var publisher = provider.GetKeyedService<IOutputPublisher>(outputKey) ?? compiled.Publisher;
        var input = provider.GetKeyedService<IInputCoordinator>(inputKey) ?? compiled.Input;
        return input == compiled.Input && publisher == compiled.Publisher
            ? compiled
            : new AgentRunServices(
                compiled.Session,
                compiled.SecurityProfileSelector,
                compiled.Context,
                compiled.Tools,
                compiled.Models,
                compiled.ModelSelector,
                compiled.ModelResolver,
                compiled.ContinuationPolicy,
                compiled.OutputProcessor,
                compiled.Compactor,
                compiled.Budgets,
                compiled.RunCoordinator,
                input,
                publisher);
    }

    private static AgentRunRejected<TOutput> Reject<TOutput>(
        AgentId agentId,
        SessionId sessionId,
        AgentErrorCode code,
        string reason,
        bool retryable = false) =>
        new(agentId, sessionId, new AgentError(
            code,
            reason,
            retryable,
            SideEffectCertainty.DefinitelyNotPerformed,
            new ErrorOrigin("AgentKit.AgentEngineRuntime"),
            externalCode: null,
            operationId: null,
            externalRequestId: null,
            retryAfter: null,
            ExtensionData.Empty));

    private static AgentErrorCode Classify(string reason)
    {
        return reason.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("not visible", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            ? AgentErrorCodes.AuthorizationDenied
            : reason.Contains("busy", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("occupied", StringComparison.OrdinalIgnoreCase)
                ? AgentErrorCodes.SessionBusy
                : reason.Contains("output", StringComparison.OrdinalIgnoreCase)
                    ? AgentErrorCodes.IncompatibleSchema
                    : AgentErrorCodes.InvalidState;
    }

    private static async Task DisposeOwnedProviderAsync(IAsyncDisposable? ownedProvider)
    {
        if (ownedProvider is not null)
        {
            await ownedProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ExecutionOutcome<TOutput>
    {
        internal ExecutionOutcome(AgentLoopResult loopResult) => LoopResult = loopResult;

        internal ExecutionOutcome(AgentRunFinished<TOutput> finished) => Finished = finished;

        internal ExecutionOutcome(AgentRunRejected<TOutput> rejection) => Rejection = rejection;

        internal ExecutionOutcome(IAgentRunStream<TOutput> stream) => Stream = stream;

        internal AgentLoopResult? LoopResult { get; }

        internal AgentRunFinished<TOutput>? Finished { get; }

        internal AgentRunRejected<TOutput>? Rejection { get; }

        internal IAgentRunStream<TOutput>? Stream { get; }
    }

    /// <summary>Serializes one process's runs for a single agent session according to <see cref="SessionBusyBehavior"/>.</summary>
    private sealed class InProcessSessionGate
    {
        private readonly Lock _mutex = new();
        private readonly Dictionary<(AgentId AgentId, SessionId SessionId), Lane> _lanes = [];

        /// <summary>Tries to enter the gate for one agent session.</summary>
        /// <param name="agentId">The agent whose session is being serialized.</param>
        /// <param name="sessionId">The session being serialized.</param>
        /// <param name="busyBehavior">Whether a busy session rejects immediately or waits.</param>
        /// <param name="cancellationToken">Cancels a wait. A rejected attempt does not wait.</param>
        /// <returns>An acquisition that either holds a lease or names the blocking run.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled while waiting.</exception>
        internal async ValueTask<Acquisition> TryEnterAsync(
            AgentId agentId,
            SessionId sessionId,
            SessionBusyBehavior busyBehavior,
            CancellationToken cancellationToken)
        {
            var key = (agentId, sessionId);
            Lane lane;
            lock (_mutex)
            {
                if (!_lanes.TryGetValue(key, out lane!))
                {
                    lane = new Lane();
                    _lanes[key] = lane;
                }

                lane.Interested++;
            }

            try
            {
                if (busyBehavior == SessionBusyBehavior.Reject)
                {
                    if (!lane.Gate.Wait(0, CancellationToken.None))
                    {
                        var blocking = lane.ActiveRunId;
                        Release(key, lane, held: false);
                        return new Acquisition(false, blocking, null);
                    }
                }
                else
                {
                    await lane.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                Release(key, lane, held: false);
                throw;
            }

            return new Acquisition(true, default, new Lease(this, key, lane));
        }

        private void Release((AgentId, SessionId) key, Lane lane, bool held)
        {
            lock (_mutex)
            {
                if (held)
                {
                    lane.ActiveRunId = default;
                    _ = lane.Gate.Release();
                }

                lane.Interested--;
                if (lane.Interested == 0 && _lanes.TryGetValue(key, out var current) && ReferenceEquals(current, lane))
                {
                    _ = _lanes.Remove(key);
                    lane.Gate.Dispose();
                }
            }
        }

        /// <summary>The outcome of one attempt to enter the in-process session gate.</summary>
        /// <param name="Acquired">Whether this caller now holds the gate.</param>
        /// <param name="BlockingRunId">The run already holding the gate when acquisition was rejected, or the default value.</param>
        /// <param name="Held">The lease that releases the gate, or <see langword="null"/> when acquisition failed.</param>
        internal readonly record struct Acquisition(bool Acquired, RunId BlockingRunId, Lease? Held);

        /// <summary>One session's in-process serializer and the run currently holding it.</summary>
        internal sealed class Lane
        {
            /// <summary>Gets the mutex that admits one run at a time for this session.</summary>
            internal SemaphoreSlim Gate { get; } = new(1, 1);

            /// <summary>Gets or sets the run identity currently holding the gate, once that identity exists.</summary>
            internal RunId ActiveRunId { get; set; }

            /// <summary>Gets or sets how many callers are waiting on or holding this lane.</summary>
            internal int Interested { get; set; }
        }

        /// <summary>Releases one acquired in-process gate exactly once.</summary>
        internal sealed class Lease: IDisposable
        {
            private readonly InProcessSessionGate _owner;
            private readonly (AgentId, SessionId) _key;
            private Lane? _lane;

            /// <summary>Initializes a lease over one acquired lane.</summary>
            /// <param name="owner">The gate that owns the lane.</param>
            /// <param name="key">The agent and session the lane belongs to.</param>
            /// <param name="lane">The lane this lease will release.</param>
            internal Lease(InProcessSessionGate owner, (AgentId, SessionId) key, Lane lane)
            {
                _owner = owner;
                _key = key;
                _lane = lane;
            }

            /// <summary>Records the accepted run identity so a rejected waiter can name it.</summary>
            /// <param name="runId">The run that now holds the gate.</param>
            internal void AssignRun(RunId runId)
            {
                if (_lane is { } lane)
                {
                    lane.ActiveRunId = runId;
                }
            }

            /// <summary>Releases the gate and removes the lane when no caller remains interested.</summary>
            public void Dispose()
            {
                var lane = Interlocked.Exchange(ref _lane, null);
                if (lane is not null)
                {
                    _owner.Release(_key, lane, held: true);
                }
            }
        }
    }
}
