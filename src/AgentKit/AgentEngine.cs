// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Represents an immutable process-level AgentKit composition that hosts a
/// versioned catalog of agent definitions and runs them concurrently.
/// </summary>
/// <remarks>
/// <para>
/// The engine is not an agent. It carries no agent's mutable state and never
/// makes a process-wide singleton out of a run scope. Each invocation creates
/// an isolated dependency-injection scope and a fresh <see cref="RunId"/>.
/// </para>
/// <para>
/// An engine created by <see cref="AgentEngineBuilder.Build"/> owns the
/// service provider captured by that build and disposes it exactly once. An
/// engine resolved from a host-owned provider does not own or dispose that
/// provider.
/// </para>
/// <para>
/// The container is never exposed. Callers resolve agents by
/// <see cref="AgentId"/> and run them through the returned
/// <see cref="Agent"/> handle, so no caller can bypass definition resolution
/// or substitute a component the composition did not validate.
/// </para>
/// </remarks>
public sealed class AgentEngine: IAsyncDisposable
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
    private readonly SessionLaneRegistry _lanes = new();
    private readonly ILogger<AgentEngine> _logger;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Initializes an engine over one captured composition.
    /// </summary>
    /// <param name="services">
    /// The composition the engine resolves scoped run services from.
    /// </param>
    /// <param name="ownedProvider">
    /// The standalone provider owned by this engine, or <see langword="null"/>
    /// when an external host owns the provider.
    /// </param>
    /// <param name="validatedComposition">
    /// The exact immutable readiness evidence already inspected by composition validation.
    /// Engine construction pins this supplied value without consulting replaceable readers again.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="validatedComposition"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The composition does not contain the engine-wide services the facade
    /// requires.
    /// </exception>
    internal AgentEngine(
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
        ComponentRegistrations = validatedComposition.ComponentRegistrations;
        _pinnedRunProfiles = validatedComposition.RunProfiles.Publications.ToImmutableDictionary(
            static publication => (
                publication.SecurityProfile.AgentId,
                publication.SecurityProfile.AgentDefinitionRevision));
        _logger = services.GetService<ILogger<AgentEngine>>() ?? NullLogger<AgentEngine>.Instance;
    }

    /// <summary>
    /// Gets the engine-wide clock captured at composition time.
    /// </summary>
    /// <value>
    /// The singleton <see cref="System.TimeProvider"/> selected by the
    /// standalone builder or external host. The reference never changes for
    /// this engine.
    /// </value>
    /// <summary>
    /// Gets the composed service provider so the application that built this engine can reach the
    /// services it registered, in the same way an <c>IHost</c> exposes its services.
    /// </summary>
    /// <remarks>
    /// This is a composition-root surface for the application only. Runtime components never receive
    /// it: every framework collaborator is injected through its constructor. The provider's lifetime is
    /// the engine's; for a standalone engine it is disposed with the engine, and for a host-managed engine
    /// it is the host's provider.
    /// </remarks>
    public IServiceProvider Services { get; }

    internal TimeProvider TimeProvider { get; }

    /// <summary>Gets the exact partial component-registration evidence validated for this engine.</summary>
    /// <value>The build-local immutable snapshot; it never changes when a builder collection is later mutated.</value>
    internal ComponentRegistrationSnapshot ComponentRegistrations { get; }

    /// <summary>
    /// Creates a mutable builder for a new standalone engine composition.
    /// </summary>
    /// <returns>
    /// A new builder with an independent service collection and the AgentKit
    /// facade defaults registered.
    /// </returns>
    public static AgentEngineBuilder CreateBuilder() => new();

    /// <summary>
    /// Lists every agent this engine currently hosts.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// The immutable definitions in the current catalog snapshot, in
    /// composition order.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    /// <exception cref="ObjectDisposedException">This engine has already been disposed.</exception>
    public async ValueTask<ImmutableArray<AgentDefinition>> GetAgentsAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var snapshot = await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Definitions;
    }

    /// <summary>
    /// Resolves one hosted agent to a runnable handle.
    /// </summary>
    /// <param name="agentId">The agent identity to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// A handle over the resolved definition, or <see langword="null"/> when
    /// this engine hosts no such agent.
    /// </returns>
    /// <remarks>
    /// An unknown identity returns <see langword="null"/> rather than
    /// throwing, because agent identities routinely arrive from outside the
    /// process and a host should be able to answer "no such agent" without
    /// catching.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The agent exists but its definition cannot be used with this
    /// composition.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    /// <exception cref="ObjectDisposedException">This engine has already been disposed.</exception>
    public async ValueTask<Agent?> GetAgentAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var resolution = await _catalog.ResolveAsync(agentId, cancellationToken).ConfigureAwait(false);

        return resolution switch
        {
            ResolvedAgentDefinition resolved =>
                new Agent(this, resolved.Definition, resolved.CatalogVersion),
            AgentDefinitionNotFound => null,
            InvalidAgentDefinition invalid => throw new InvalidOperationException(
                $"Agent '{agentId}' is hosted but its definition is unusable: "
                + string.Join("; ", invalid.Diagnostics)),
            _ => throw new InvalidOperationException(
                $"Unrecognized {nameof(AgentDefinitionResolution)} kind '{resolution.GetType()}'."),
        };
    }

    /// <summary>
    /// Releases the standalone service provider owned by this engine.
    /// </summary>
    /// <returns>
    /// An operation that completes when owned services have finished
    /// asynchronous disposal. Hosted engines complete without disposing any
    /// host-owned service.
    /// </returns>
    /// <remarks>
    /// Disposal is thread-safe and idempotent. Concurrent and subsequent calls
    /// observe the same disposal operation, so the owned provider is disposed
    /// at most once.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            _disposed = true;
            _disposeTask ??= DisposeOwnedProviderAsync(_ownedProvider);
            return new ValueTask(_disposeTask);
        }
    }

    /// <summary>
    /// Admits one user turn for an agent: creates or opens the session, takes the session's lane, appends the
    /// user message, and runs the agent in a fresh, isolated run scope.
    /// </summary>
    /// <param name="definition">The immutable definition to run.</param>
    /// <param name="request">The identity, message, session selection, overrides, and observer for this turn.</param>
    /// <param name="cancellationToken">
    /// Cancels the caller's wait. Before the user message is committed, cancellation propagates and leaves no
    /// durable effect; afterwards the loop settles the run with a typed cancelled outcome that is returned.
    /// </param>
    /// <returns>The loop's terminal result, naming the session, branch, and run the turn was recorded against.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An override in <paramref name="request"/> is wider than the definition's default.</exception>
    /// <exception cref="AgentAdmissionRejectedException">
    /// The catalog no longer enables the exact pinned definition, the run-profile publication or authorization
    /// changed after validation, the requested session does not exist or is not owned by the requesting agent,
    /// tenant, and principal, or the session could not be created, loaded, or appended to. Raised before any run
    /// effect other than, at most, a newly created empty session.
    /// </exception>
    /// <exception cref="AgentSessionBusyException">
    /// The session is running another turn and the pinned session profile rejects concurrent turns.
    /// </exception>
    /// <exception cref="ObjectDisposedException">This engine has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// The session lane is entered before anything is appended, so two turns racing for the same session either
    /// serialize (<see cref="SessionBusyBehavior.Wait"/>) or the loser fails without effect
    /// (<see cref="SessionBusyBehavior.Reject"/>). Different sessions, of the same or different agents, run
    /// concurrently. The lane is process-local; distributed exclusion is the session run coordinator's lease.
    /// </para>
    /// <para>
    /// The user message is appended with the run's own identity and an idempotency key derived from it, so a retried
    /// append cannot duplicate the message. The run then observes that message as the newest history entry.
    /// </para>
    /// </remarks>
    internal async Task<AgentLoopResult> SendAgentAsync(
        AgentDefinition definition,
        AgentSendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var activity = AgentAdmissionObservability.Start(definition.Id);
        var admissionCompleted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (catalogVersion, pinnedPublication) = await ValidatePinnedDefinitionAsync(definition, activity, cancellationToken)
                .ConfigureAwait(false);
            var maxTurns = ResolveMaxTurns(definition, request.MaxTurns, nameof(request));
            var attemptTimeout = ResolveAttemptTimeout(definition, request.AttemptTimeout, nameof(request));
            var security = pinnedPublication.SecurityProfile;
            var sessionProfile = pinnedPublication.SessionProfile;

            await using var scope = Services.CreateAsyncScope();
            var loopKey = definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey;
            var loop = scope.ServiceProvider.GetRequiredKeyedService<IAgentLoop>(loopKey.Value);
            var runServices = AgentRunServicesFactory.Compile(scope.ServiceProvider, loopKey);
            var sessions = runServices.Session;

            var descriptor = request.SessionId is { } requested
                ? await OpenSessionAsync(definition, catalogVersion, security, sessionProfile, sessions, requested, request.Identity, cancellationToken).ConfigureAwait(false)
                : await CreateSessionAsync(definition, catalogVersion, security, sessionProfile, sessions, request.Identity, cancellationToken).ConfigureAwait(false);
            var sessionId = descriptor.Address.SessionId;
            var branchId = descriptor.ActiveBranchId;
            _ = activity?.SetTag(AgentKitTagNames.SessionId, sessionId.ToString());

            var runId = _runIds.Create();
            using var lane = await _lanes.EnterAsync(definition.Id, sessionId, runId, sessionProfile.BusyBehavior, cancellationToken)
                .ConfigureAwait(false);
            var runCorrelation = new InRunOperationCorrelation(_operationIds.Create(), runId, turnId: null);
            var authorization = await CaptureAsync(
                definition, catalogVersion, security, sessionId, runCorrelation, request.Identity, cancellationToken).ConfigureAwait(false);
            var context = new SessionOperationContext(definition.Id, sessionId, null, runCorrelation, request.Identity, authorization);

            var readResult = await sessions.ReadAsync(
                new SessionReadRequest(context, branchId, new SessionSequence(0), pageSize: 1), sessionProfile, cancellationToken)
                .ConfigureAwait(false);
            if (readResult is not SessionPage { Snapshot: { } snapshot })
            {
                throw AdmissionRejected(definition, catalogVersion, "The session's current history snapshot could not be captured.");
            }

            var now = TimeProvider.GetUtcNow();
            var userMessage = new UserMessage(
                _messageIds.Create(), definition.Id, sessionId, descriptor.ConversationId, branchId, runId, null, now,
                MessageState.Complete, request.Parts, ExtensionData.Empty);
            var appendResult = await sessions.AppendAsync(
                new SessionAppendRequest(
                    context,
                    branchId,
                    snapshot.Version,
                    new IdempotencyKey($"agentkit.engine:{runId}:user"),
                    [
                        new MessageSessionEntry(
                            _entryIds.Create(), new SessionAddress(definition.Id, sessionId), runCorrelation, branchId,
                            new SessionSequence(snapshot.UpperSequence.Value + 1), null, now, new SchemaVersion("1"), userMessage),
                    ]),
                sessionProfile,
                cancellationToken).ConfigureAwait(false);
            if (appendResult is not SessionAppended)
            {
                throw AdmissionRejected(definition, catalogVersion, $"The user message could not be recorded: {appendResult.GetType().Name}.");
            }

            var runRequest = BuildRunRequest(
                definition, pinnedPublication, sessionId, branchId, runId, request.Identity, authorization, maxTurns, attemptTimeout) with
            {
                Observer = request.Observer,
            };

            AgentAdmissionObservability.Complete(activity, _logger, "admitted");
            activity = null;
            admissionCompleted = true;
            return await loop.RunAsync(runRequest, runServices, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!admissionCompleted && cancellationToken.IsCancellationRequested)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "cancelled", nameof(OperationCanceledException));
            AgentAdmissionObservability.LogCancelled(_logger);
            throw;
        }
        catch (AgentSessionBusyException) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "busy", nameof(AgentSessionBusyException));
            activity = null;
            admissionCompleted = true;
            throw;
        }
        catch (AgentAdmissionRejectedException) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "rejected", nameof(AgentAdmissionRejectedException));
            activity = null;
            admissionCompleted = true;
            throw;
        }
        catch (Exception exception) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "failed", exception.GetType().Name);
            AgentAdmissionObservability.LogFailed(_logger, exception.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Runs one agent in a fresh, isolated run scope.
    /// </summary>
    /// <param name="definition">The immutable definition to run.</param>
    /// <param name="options">The per-invocation facts and bounded overrides.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>The loop's terminal result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An override in <paramref name="options"/> is wider than the
    /// definition's corresponding default.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/> or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="AgentAdmissionRejectedException">
    /// The current catalog does not enable <paramref name="definition"/>'s
    /// exact pinned content and revision. The exception is raised before a
    /// run identity or scope is created.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This engine has already been disposed. Raised before a run identity is minted or
    /// authorization is captured, on both the standalone and host-managed ownership paths.
    /// </exception>
    internal async Task<AgentLoopResult> RunAgentAsync(
        AgentDefinition definition,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var activity = AgentAdmissionObservability.Start(definition.Id);
        var admissionCompleted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (pinnedCatalogVersion, pinnedPublication) = await ValidatePinnedDefinitionAsync(definition, activity, cancellationToken)
                .ConfigureAwait(false);
            var maxTurns = ResolveMaxTurns(definition, options);
            var attemptTimeout = ResolveAttemptTimeout(definition, options);

            var runId = _runIds.Create();
            var runCorrelation = new InRunOperationCorrelation(_operationIds.Create(), runId, turnId: null);
            var authorization = await CaptureAsync(
                definition, pinnedCatalogVersion, pinnedPublication.SecurityProfile, options.SessionId, runCorrelation, options.Identity, cancellationToken)
                .ConfigureAwait(false);

            await using var scope = Services.CreateAsyncScope();
            var loopKey = definition.LoopKey ?? AgentLoopComponentDefaults.LoopKey;
            var loop = scope.ServiceProvider.GetRequiredKeyedService<IAgentLoop>(loopKey.Value);
            var runServices = AgentRunServicesFactory.Compile(scope.ServiceProvider, loopKey);
            var request = BuildRunRequest(
                definition, pinnedPublication, options.SessionId, options.BranchId, runId, options.Identity, authorization, maxTurns, attemptTimeout);

            AgentAdmissionObservability.Complete(activity, _logger, "admitted");
            activity = null;
            admissionCompleted = true;
            return await loop.RunAsync(request, runServices, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!admissionCompleted && cancellationToken.IsCancellationRequested)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "cancelled", nameof(OperationCanceledException));
            AgentAdmissionObservability.LogCancelled(_logger);
            throw;
        }
        catch (AgentAdmissionRejectedException) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(
                activity, _logger, "rejected", nameof(AgentAdmissionRejectedException));
            activity = null;
            admissionCompleted = true;
            throw;
        }
        catch (Exception exception) when (!admissionCompleted)
        {
            AgentAdmissionObservability.Complete(activity, _logger, "failed", exception.GetType().Name);
            AgentAdmissionObservability.LogFailed(_logger, exception.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Revalidates that the catalog still enables the exact pinned definition and that its run-profile publication
    /// is unchanged since composition.
    /// </summary>
    /// <param name="definition">The pinned definition.</param>
    /// <param name="activity">The admission activity to tag with the catalog version, or null.</param>
    /// <param name="cancellationToken">Cancels the catalog and publication reads.</param>
    /// <returns>The current catalog version and the pinned publication.</returns>
    /// <exception cref="AgentAdmissionRejectedException">The definition or its publication changed.</exception>
    private async Task<(AgentCatalogVersion CatalogVersion, AgentRunProfilePublication Publication)> ValidatePinnedDefinitionAsync(
        AgentDefinition definition,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        Debug.Assert(definition is not null, "Callers validate the definition.");
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

    /// <summary>Captures fresh authorization for one operation scope and verifies it matches the pinned publication.</summary>
    private async Task<SecurityAuthorizationContext> CaptureAsync(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        SecurityProfilePublication security,
        SessionId? sessionId,
        OperationCorrelation correlation,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        Debug.Assert(security is not null, "The pinned publication carries the security profile.");
        var result = await _securityProfiles.SelectAsync(
            new SecurityAuthorizationCaptureRequest(
                new SecurityAuthorizationScope(definition.Id, sessionId, correlation),
                security.ProfileKey,
                security.AgentDefinitionRevision,
                security.ConfigurationVersion,
                identity),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result is SecurityAuthorizationCaptured captured
            && Matches(captured.Authorization, security, correlation, sessionId, identity)
            ? captured.Authorization
            : throw AdmissionRejected(definition, catalogVersion, "Fresh authorization did not match the pinned security publication.");
    }

    /// <summary>Creates a new session owned by <paramref name="identity"/> for the agent.</summary>
    private async Task<SessionDescriptor> CreateSessionAsync(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        SecurityProfilePublication security,
        SessionProfileSnapshot sessionProfile,
        ISessionCoordinator sessions,
        ExecutionIdentity identity,
        CancellationToken cancellationToken)
    {
        var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
        var authorization = await CaptureAsync(definition, catalogVersion, security, null, correlation, identity, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Loads an existing session and verifies the agent, tenant, and principal own it.</summary>
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
        var correlation = new BeforeRunOperationCorrelation(_operationIds.Create(), null);
        var authorization = await CaptureAsync(definition, catalogVersion, security, sessionId, correlation, identity, cancellationToken).ConfigureAwait(false);
        var result = await sessions.LoadAsync(
            new SessionOperationContext(definition.Id, sessionId, null, correlation, identity, authorization),
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

    /// <summary>Builds the loop request from the pinned publication, preferring the configuration-bearing constructor.</summary>
    private static AgentRunRequest BuildRunRequest(
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
            ? new AgentRunRequest(
                definition, sessionId, branchId, runId, identity, authorization, pinnedPublication.SessionProfile,
                configuration, maxTurns, attemptTimeout, definition.Extensions)
            : new AgentRunRequest(
                definition.Id, sessionId, branchId, runId, identity, authorization, pinnedPublication.SessionProfile,
                definition.Models, definition.ModelRequirements, definition.Instructions, definition.Tools,
                definition.ToolChoice, definition.Settings, maxTurns, attemptTimeout, definition.Extensions)
            {
                Output = definition.Output,
            };

    private static AgentAdmissionRejectedException AdmissionRejected(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion,
        string reason) => new(new AgentAdmissionRejection(definition.Id, definition.Revision, catalogVersion, reason));

    private static bool Matches(
        SecurityAuthorizationContext authorization,
        SecurityProfilePublication publication,
        OperationCorrelation correlation,
        SessionId? sessionId,
        ExecutionIdentity identity)
    {
        Debug.Assert(authorization is not null, "Captured authorization is required for evidence matching.");
        Debug.Assert(publication is not null, "Pinned security publication is required for evidence matching.");

        return authorization.Scope.AgentId == publication.AgentId
            && authorization.Scope.SessionId == sessionId
            && authorization.Scope.Correlation == correlation
            && authorization.Identity == identity
            && authorization.ProfileKey == publication.ProfileKey
            && authorization.ProfileVersion == publication.ProfileVersion
            && authorization.PolicySnapshot == publication.PolicySnapshot
            && authorization.AuthorityKey == publication.AuthorityKey
            && authorization.AgentDefinitionRevision == publication.AgentDefinitionRevision
            && authorization.ConfigurationVersion == publication.ConfigurationVersion;
    }

    private static int ResolveMaxTurns(AgentDefinition definition, AgentRunOptions options) =>
        ResolveMaxTurns(definition, options.MaxTurns, nameof(options));

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

    private static TimeSpan ResolveAttemptTimeout(AgentDefinition definition, AgentRunOptions options) =>
        ResolveAttemptTimeout(definition, options.AttemptTimeout, nameof(options));

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

    private static async Task DisposeOwnedProviderAsync(IAsyncDisposable? ownedProvider)
    {
        if (ownedProvider is not null)
        {
            await ownedProvider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
