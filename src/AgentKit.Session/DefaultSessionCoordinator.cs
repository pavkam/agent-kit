// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using Microsoft.Extensions.Options;

/// <summary>Routes logical session operations through separately authorized directory and selected-store effects.</summary>
/// <remarks>The coordinator forwards grants without consuming them. Directory and store implementations remain the effecting boundaries that validate required audit and consume each exact grant.</remarks>
internal sealed class DefaultSessionCoordinator: ISessionCoordinator
{
    private static readonly SchemaVersion _directorySchemaVersion = new("agentkit.session-directory/v1");
    private readonly ISessionDirectory _directory;
    private readonly ISessionStoreSelector _storeSelector;
    private readonly ISecurityProfileSelector _profileSelector;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SessionId> _sessionIds;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ImmutableArray<ISessionEventSink> _eventSinks;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumAppendEntries;
    private readonly int _maximumPageSize;
    private readonly TimeSpan _securityRequestLifetime;
    private readonly ILogger<DefaultSessionCoordinator> _logger;

    /// <summary>Initializes a stateless default coordinator over explicit routing and security collaborators.</summary>
    /// <param name="directory">The authoritative protected session-route directory.</param>
    /// <param name="storeSelector">The exact-key selector over explicitly composed stores.</param>
    /// <param name="profileSelector">Captures fresh session-bound authorization after create routing establishes a session identity.</param>
    /// <param name="authoritySelector">Activates only the authority retained by captured authorization evidence.</param>
    /// <param name="sessionIds">Allocates session identities only after a creation-route miss and successful initial store selection.</param>
    /// <param name="securityRequestIds">Creates identities for independently authorized directory and store requests.</param>
    /// <param name="intentIds">Creates stable consumption-intent identities forwarded to effecting boundaries.</param>
    /// <param name="eventSinks">The additive ordered best-effort semantic-event sinks.</param>
    /// <param name="timeProvider">The clock used for authorization deadlines, route evidence, and semantic events.</param>
    /// <param name="options">The validated process-wide hard ceilings.</param>
    /// <param name="logger">The optional content-free diagnostics logger.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    public DefaultSessionCoordinator(
        ISessionDirectory directory,
        ISessionStoreSelector storeSelector,
        ISecurityProfileSelector profileSelector,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SessionId> sessionIds,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IEnumerable<ISessionEventSink> eventSinks,
        TimeProvider timeProvider,
        IOptions<AgentSessionOptions> options,
        ILogger<DefaultSessionCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(storeSelector);
        ArgumentNullException.ThrowIfNull(profileSelector);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(sessionIds);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(eventSinks);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _directory = directory;
        _storeSelector = storeSelector;
        _profileSelector = profileSelector;
        _authoritySelector = authoritySelector;
        _sessionIds = sessionIds;
        _securityRequestIds = securityRequestIds;
        _intentIds = intentIds;
        _eventSinks = [.. eventSinks];
        _timeProvider = timeProvider;
        _maximumAppendEntries = options.Value.MaximumAppendEntries;
        _maximumPageSize = options.Value.MaximumPageSize;
        _securityRequestLifetime = options.Value.SecurityRequestLifetime;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSessionCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        return ObserveAsync(AgentKitActivityNames.SessionCreate, request.AgentId, null,
            request.Authorization.Scope.Correlation.OperationId,
            () => CreateCoreAsync(request, profile, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(profile);
        return ObserveAsync(AgentKitActivityNames.SessionLoad, context.AgentId, context.SessionId,
            context.Correlation.OperationId,
            () => ExecuteExistingAsync(context, context, profile, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                SessionStoreSecurityBinding.Fingerprint(context),
                static (store, wrapper, token) => store.LoadAsync(wrapper, token),
                static reason => new SessionLoadFailed(reason), cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryListResult> ListAsync(
        SessionDirectoryListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync(
            AgentKitActivityNames.SessionDirectoryList,
            request.AgentId,
            null,
            request.Authorization.Scope.Correlation.OperationId,
            () => ListCoreAsync(request, cancellationToken),
            cancellationToken);
    }

    /// <summary>Authorizes and executes one bounded directory scan without selecting or probing stores.</summary>
    private async ValueTask<SessionDirectoryListResult> ListCoreAsync(
        SessionDirectoryListRequest request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public list boundary validates the request.");
        var resource = SessionDirectorySecurityBinding.ListResource(request.Identity.TenantId, request.AgentId);
        var grant = await AuthorizeAsync(
            request.Authorization,
            _directory.SecurityAudience,
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            resource,
            SessionDirectorySecurityBinding.ListFingerprint(request),
            cancellationToken).ConfigureAwait(false);
        return grant is null
            ? new SessionDirectoryListUnavailable("Session discovery was not authorized.")
            : await _directory.ListAsync(DirectoryRequest(request, grant), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        var maximum = Math.Min(_maximumAppendEntries, profile.MaximumAppendEntries);
        return ObserveAsync(AgentKitActivityNames.SessionCommit, request.Context.AgentId, request.Context.SessionId,
            request.Context.Correlation.OperationId, async () =>
            {
                if (request.Entries.Length > maximum)
                {
                    return new SessionAppendFailed(
                        $"Append request carries {request.Entries.Length} entries, exceeding the configured maximum of {maximum}.");
                }

                var result = await ExecuteExistingAsync(request, request.Context, profile,
                    SecurityOperationKind.StateMutation, SecurityEffect.Append,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.AppendAsync(wrapper, token),
                    static reason => new SessionAppendFailed(reason), cancellationToken).ConfigureAwait(false);
                if (result is SessionAppended appended)
                {
                    await PublishBestEffortAsync(
                        () => new SessionAppendedEvent(
                            request.Context.ToAddress(), _timeProvider.GetUtcNow(), request.BranchId,
                            appended.NewVersion, appended.CommittedEntries.Length)).ConfigureAwait(false);
                }
                return result;
            }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        var maximum = Math.Min(_maximumPageSize, profile.MaximumPageSize);
        return ObserveAsync(AgentKitActivityNames.SessionRead, request.Context.AgentId, request.Context.SessionId,
            request.Context.Correlation.OperationId,
            () => request.PageSize > maximum
                ? ValueTask.FromResult<SessionPageResult>(new SessionReadFailed(
                    $"Requested page size {request.PageSize} exceeds the configured maximum of {maximum}."))
                : ExecuteExistingAsync(request, request.Context, profile, SecurityOperationKind.StateRead,
                    SecurityEffect.Observe, SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.ReadAsync(wrapper, token),
                    static reason => new SessionReadFailed(reason), cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        return ObserveAsync(AgentKitActivityNames.SessionBranch, request.Context.AgentId, request.Context.SessionId,
            request.Context.Correlation.OperationId, async () =>
            {
                var result = await ExecuteExistingAsync(request, request.Context, profile,
                    SecurityOperationKind.StateMutation, SecurityEffect.Create,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.CreateBranchAsync(wrapper, token),
                    static reason => new SessionBranchFailed(reason), cancellationToken).ConfigureAwait(false);
                if (result is SessionBranched branched)
                {
                    await PublishBestEffortAsync(
                        () => new SessionBranchedEvent(
                            request.Context.ToAddress(), _timeProvider.GetUtcNow(), request.ParentBranchId,
                            branched.NewBranchId, branched.ForkedAtSequence)).ConfigureAwait(false);
                }
                return result;
            }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        return ObserveAsync(AgentKitActivityNames.SessionDelete, request.Context.AgentId, request.Context.SessionId,
            request.Context.Correlation.OperationId, async () =>
            {
                var result = await ExecuteExistingAsync(request, request.Context, profile,
                    SecurityOperationKind.StateMutation, SecurityEffect.Delete,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.DeleteAsync(wrapper, token),
                    static reason => new SessionDeleteFailed(reason), cancellationToken).ConfigureAwait(false);
                if (result is SessionDeleted)
                {
                    await PublishBestEffortAsync(
                        () => new SessionDeletedEvent(request.Context.ToAddress(), _timeProvider.GetUtcNow())).ConfigureAwait(false);
                }
                return result;
            }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionInputLookupResult> LookupInputAsync(SessionInputLookupRequest request,
        SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return ObserveAsync(AgentKitActivityNames.SessionInputLookup, request.Context,
            () => !ReferenceEquals(session.Coordinator, this)
                ? ValueTask.FromResult<SessionInputLookupResult>(
                    new SessionInputLookupRejected("The compiled capability selected a different session coordinator."))
                : ExecuteExistingAsync(request, request.Context, session.Profile, SecurityOperationKind.StateRead,
                SecurityEffect.Observe, SessionStoreSecurityBinding.Fingerprint(request),
                static (store, wrapper, token) => store.LookupInputAsync(wrapper, token),
                static reason => new SessionInputLookupRejected(reason), cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
        SessionExecutionLaneProvisionRequest request, SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return ObserveAsync(AgentKitActivityNames.SessionLaneProvision, request.Context,
            () => !ReferenceEquals(session.Coordinator, this)
                ? ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                    new SessionExecutionLaneProvisionRejected(
                        "The compiled capability selected a different session coordinator."))
                : ExecuteExistingAsync(request, request.Context, session.Profile,
                    SecurityOperationKind.StateMutation, SecurityEffect.Create,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.ProvisionLaneAsync(wrapper, token),
                    static reason => new SessionExecutionLaneProvisionRejected(reason), cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<InputAdmissionResult> AdmitInputAsync(SessionInputAdmissionRequest request,
        SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return ObserveAsync(AgentKitActivityNames.SessionInputAdmit, request.Context,
            () => !ReferenceEquals(session.Coordinator, this)
                ? ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(new InputRejection(
                    InputRejectionKind.Unauthorized,
                    "The compiled capability selected a different session coordinator.")))
                : ExecuteExistingAsync(request, request.Context, session.Profile,
                    SecurityOperationKind.StateMutation, SecurityEffect.Append,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.AdmitInputAsync(wrapper, token),
                    static reason => new RejectedInput(new InputRejection(InputRejectionKind.Unauthorized, reason)),
                    cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionRunStartResult> AcceptRunAsync(SessionRunStartRequest request,
        SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return ObserveAsync(AgentKitActivityNames.SessionRunAccept, request.Context,
            () => !ReferenceEquals(session.Coordinator, this)
                ? ValueTask.FromResult<SessionRunStartResult>(new SessionRunStartRejected(
                    "The compiled capability selected a different session coordinator."))
                : ExecuteExistingAsync(request, request.Context, session.Profile,
                    SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.AcceptRunAsync(wrapper, token),
                    static reason => new SessionRunStartRejected(reason), cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(SessionRunStateRequest request,
        SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return ObserveAsync(AgentKitActivityNames.SessionRunStateLoad, request.Context,
            () => !ReferenceEquals(session.Coordinator, this)
                ? ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateUnavailable(
                    "The compiled capability selected a different session coordinator."))
                : ExecuteExistingAsync(request, request.Context, session.Profile,
                    SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    SessionStoreSecurityBinding.Fingerprint(request),
                    static (store, wrapper, token) => store.LoadRunStateAsync(wrapper, token),
                    static reason => new SessionRunStateUnavailable(reason), cancellationToken), cancellationToken);
    }

    private async ValueTask<SessionCreateResult> CreateCoreAsync(SessionCreateRequest request,
        SessionProfileSnapshot profile, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public create boundary validates the request.");
        Debug.Assert(profile is not null, "The public create boundary validates the profile.");
        if (profile.RequiresDurableStore && !_directory.Durable)
        {
            return new SessionCreateFailed("The session directory cannot satisfy the profile's durability requirement.");
        }

        var creationResource = SessionDirectorySecurityBinding.CreationResource(
            request.Identity.TenantId, request.AgentId, request.IdempotencyKey);
        var lookupGrant = await AuthorizeAsync(request.Authorization, _directory.SecurityAudience,
            SecurityOperationKind.StateRead, SecurityEffect.Observe, creationResource,
            SessionDirectorySecurityBinding.LocateForCreateFingerprint(request), cancellationToken).ConfigureAwait(false);
        if (lookupGrant is null)
        {
            return new SessionCreateFailed("Session creation-route lookup was not authorized.");
        }

        var lookup = await _directory.LocateForCreateAsync(DirectoryRequest(request, lookupGrant),
            cancellationToken).ConfigureAwait(false);
        SessionLocation location;
        if (lookup is SessionCreationLocationLocated located)
        {
            location = located.Location;
        }
        else if (lookup is SessionCreationLocationNotFound)
        {
            var initialSelection = await _storeSelector.SelectForCreateAsync(
                new SessionStoreCreateSelectionRequest(request, profile), cancellationToken).ConfigureAwait(false);
            if (initialSelection is not SessionStoreSelected initialStore)
            {
                return new SessionCreateFailed(SafeSelectionReason(initialSelection));
            }

            var address = new SessionAddress(request.AgentId, _sessionIds.Create());
            var candidate = new SessionLocation(address, request.Identity.TenantId, initialStore.Descriptor.Key,
                new SessionDirectoryRevision(1), _timeProvider.GetUtcNow(), _directorySchemaVersion);
            var record = new SessionDirectoryCreateRecordRequest(request, candidate);
            var recordGrant = await AuthorizeAsync(request.Authorization, _directory.SecurityAudience,
                SecurityOperationKind.StateMutation, SecurityEffect.Mutate, creationResource,
                SessionDirectorySecurityBinding.RecordCreateFingerprint(record), cancellationToken).ConfigureAwait(false);
            if (recordGrant is null)
            {
                return new SessionCreateFailed("Session creation-route recording was not authorized.");
            }

            var recorded = await _directory.RecordCreateAsync(DirectoryRequest(record, recordGrant),
                cancellationToken).ConfigureAwait(false);
            if (recorded is not SessionLocationRecorded winner)
            {
                return new SessionCreateFailed(SafeDirectoryWriteReason(recorded));
            }
            location = winner.Location;
        }
        else
        {
            return new SessionCreateFailed(SafeCreationLookupReason(lookup));
        }

        var context = await CaptureCreateContextAsync(request, location, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return new SessionCreateFailed("Session-bound authorization capture is unavailable.");
        }

        var selection = await _storeSelector.ResolveExistingAsync(
            new SessionStoreSelectionRequest(context, profile, location), cancellationToken).ConfigureAwait(false);
        if (selection is not SessionStoreSelected selected)
        {
            return new SessionCreateFailed(SafeSelectionReason(selection));
        }

        var storeRequest = new SessionStoreCreateRequest(request, location.Address, context);
        var storeGrant = await AuthorizeAsync(context.Authorization, selected.Store.SecurityAudience,
            SecurityOperationKind.StateMutation, SecurityEffect.Create,
            SessionStoreSecurityBinding.Resource(selected.Descriptor.Key, location.Address),
            SessionStoreSecurityBinding.Fingerprint(storeRequest), cancellationToken).ConfigureAwait(false);
        if (storeGrant is null)
        {
            return new SessionCreateFailed("Session store creation was not authorized.");
        }

        var result = await selected.Store.CreateAsync(StoreRequest(storeRequest, selected.Descriptor.Key, storeGrant),
            cancellationToken).ConfigureAwait(false);
        if (result is SessionCreated created)
        {
            await PublishBestEffortAsync(
                () => new SessionCreatedEvent(
                    created.Descriptor.Address, _timeProvider.GetUtcNow(), created.Descriptor)).ConfigureAwait(false);
        }
        return result;
    }

    private async ValueTask<TResult> ExecuteExistingAsync<TRequest, TResult>(TRequest request,
        SessionOperationContext context, SessionProfileSnapshot profile, SecurityOperationKind kind,
        SecurityEffect effect, InputFingerprint fingerprint,
        Func<ISessionStore, AuthorizedSessionStoreRequest<TRequest>, CancellationToken, ValueTask<TResult>> operation,
        Func<string, TResult> failed, CancellationToken cancellationToken)
        where TRequest : class where TResult : class
    {
        Debug.Assert(request is not null, "A validated immutable session request is required.");
        Debug.Assert(context is not null, "A validated session context is required.");
        Debug.Assert(profile is not null, "A validated immutable profile is required.");
        Debug.Assert(operation is not null, "A store operation delegate is required.");
        Debug.Assert(failed is not null, "A typed failure factory is required.");
        if (profile.RequiresDurableStore && !_directory.Durable)
        {
            return failed("The session directory cannot satisfy the profile's durability requirement.");
        }

        var directoryGrant = await AuthorizeAsync(context.Authorization, _directory.SecurityAudience,
            SecurityOperationKind.StateRead, SecurityEffect.Observe,
            SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress()),
            SessionDirectorySecurityBinding.LocateFingerprint(context), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (directoryGrant is null)
        {
            return failed("Session route lookup was not authorized.");
        }
        var route = await _directory.LocateAsync(DirectoryRequest(context, directoryGrant),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (route is not SessionLocated located)
        {
            return failed(SafeLocationReason(route));
        }

        var selection = await _storeSelector.ResolveExistingAsync(
            new SessionStoreSelectionRequest(context, profile, located.Location), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (selection is not SessionStoreSelected selected)
        {
            return failed(SafeSelectionReason(selection));
        }

        var storeGrant = await AuthorizeAsync(context.Authorization, selected.Store.SecurityAudience, kind, effect,
            SessionStoreSecurityBinding.Resource(selected.Descriptor.Key, context.ToAddress()), fingerprint,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return storeGrant is null
            ? failed("Session store access was not authorized.")
            : await InvokeStoreAsync(operation, selected.Store,
                StoreRequest(request, selected.Descriptor.Key, storeGrant), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Invokes the selected store and returns its typed result even when the caller cancelled meanwhile.</summary>
    /// <remarks>
    /// The store decides whether cancellation prevents its effect. Once it has returned a result, that result is the
    /// committed truth and is never replaced by <see cref="OperationCanceledException"/>.
    /// </remarks>
    private static async ValueTask<TResult> InvokeStoreAsync<TRequest, TResult>(
        Func<ISessionStore, AuthorizedSessionStoreRequest<TRequest>, CancellationToken, ValueTask<TResult>> operation,
        ISessionStore store, AuthorizedSessionStoreRequest<TRequest> request, CancellationToken cancellationToken)
        where TRequest : class where TResult : class
    {
        Debug.Assert(operation is not null, "A validated store operation delegate is required.");
        Debug.Assert(store is not null, "A selected session store is required.");
        Debug.Assert(request is not null, "An authorized store request is required.");
        return await operation(store, request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SessionOperationContext?> CaptureCreateContextAsync(SessionCreateRequest request,
        SessionLocation location, CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "A validated logical create request is required.");
        Debug.Assert(location is not null, "An authoritative creation route is required.");
        var scope = new SecurityAuthorizationScope(request.AgentId, location.Address.SessionId,
            request.Authorization.Scope.Correlation);
        var captured = await _profileSelector.SelectAsync(new SecurityAuthorizationCaptureRequest(scope,
            request.Authorization.ProfileKey, request.Authorization.AgentDefinitionRevision,
            request.Authorization.ConfigurationVersion, request.Identity), cancellationToken).ConfigureAwait(false);
        if (captured is not SecurityAuthorizationCaptured successful)
        {
            return null;
        }

        var authorization = successful.Authorization;
        return authorization.Scope == scope
            && authorization.Identity == request.Identity
            && authorization.ProfileKey == request.Authorization.ProfileKey
            && authorization.ProfileVersion == request.Authorization.ProfileVersion
            && authorization.PolicySnapshot == request.Authorization.PolicySnapshot
            && authorization.AuthorityKey == request.Authorization.AuthorityKey
            && authorization.AgentDefinitionRevision == request.Authorization.AgentDefinitionRevision
            && authorization.ConfigurationVersion == request.Authorization.ConfigurationVersion
            ? new SessionOperationContext(request.AgentId, location.Address.SessionId, null,
                request.Authorization.Scope.Correlation, request.Identity, authorization)
            : null;
    }

    private async ValueTask<SecurityGrant?> AuthorizeAsync(SecurityAuthorizationContext authorization,
        ComponentId audience, SecurityOperationKind kind, SecurityEffect effect, ProtectedResource resource,
        InputFingerprint fingerprint, CancellationToken cancellationToken)
    {
        Debug.Assert(authorization is not null, "A validated captured authorization is required.");
        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return null;
        }

        var requestId = _securityRequestIds.Create();
        var decision = await selected.Authority.AuthorizeAsync(new SecurityRequest(requestId, authorization.Scope, null,
            authorization.Identity, authorization, audience, kind, effect, [resource], fingerprint,
            _timeProvider.GetUtcNow().Add(_securityRequestLifetime)), cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            && allowed.RequestId == requestId
            && allowed.Grant.RequestId == requestId
            ? allowed.Grant
            : null;
    }

    private AuthorizedSessionDirectoryRequest<TRequest> DirectoryRequest<TRequest>(TRequest request,
        SecurityGrant grant) where TRequest : class =>
        new(request, grant, new SecurityEnforcementIntent(_intentIds.Create(), null));

    private AuthorizedSessionStoreRequest<TRequest> StoreRequest<TRequest>(TRequest request, SessionStoreKey key,
        SecurityGrant grant) where TRequest : class =>
        new(request, key, grant, new SecurityEnforcementIntent(_intentIds.Create(), null));

    private ValueTask<TResult> ObserveAsync<TResult>(string operation, SessionOperationContext context,
        Func<ValueTask<TResult>> action, CancellationToken cancellationToken) where TResult : class
    {
        Debug.Assert(context is not null, "A validated session context is required.");
        return ObserveAsync(operation, context.AgentId, context.SessionId, context.Correlation.OperationId,
            action, cancellationToken, context);
    }

    private async ValueTask<TResult> ObserveAsync<TResult>(string operation, AgentId agentId, SessionId? sessionId,
        OperationId? operationId, Func<ValueTask<TResult>> action, CancellationToken cancellationToken,
        SessionOperationContext? context = null) where TResult : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A stable session operation name is required.");
        Debug.Assert(action is not null, "A session operation delegate is required.");
        var runId = context?.Correlation switch
        {
            InRunOperationCorrelation inRun => inRun.RunId,
            AfterRunOperationCorrelation afterRun => afterRun.CausalRunId,
            _ => (RunId?) null,
        };
        var turnId = (context?.Correlation as InRunOperationCorrelation)?.TurnId;
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.GenAiOperationName, operation },
            { AgentKitTagNames.TenantId, context?.Identity.TenantId.ToString() },
            { AgentKitTagNames.AgentId, agentId.ToString() },
            { AgentKitTagNames.SessionId, sessionId?.ToString() },
            { AgentKitTagNames.ExecutionLaneId, context?.ExecutionLaneId?.ToString() },
            { AgentKitTagNames.OperationId, operationId?.ToString() },
            { AgentKitTagNames.RunId, runId?.ToString() },
            { AgentKitTagNames.TurnId, turnId?.ToString() },
            { AgentKitTagNames.SessionOperation, operation },
        };
        using var activityScope = AgentKitActivityScope.Start(operation, ActivityKind.Internal, tags);
        var activity = activityScope.Activity;
        if (context is null)
        {
            TryObserve(() => SessionLog.OperationStarted(_logger, operation, agentId, sessionId));
        }
        else
        {
            TryObserve(() => SessionLog.CorrelatedOperationStarted(_logger, operation,
                context.Identity.TenantId, context.AgentId, context.SessionId, context.ExecutionLaneId,
                context.Correlation.OperationId, runId, turnId));
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action().ConfigureAwait(false);
            var outcome = Outcome(result);
            TryObserve(() =>
            {
                if (outcome == "success")
                {
                    activity.SetSuccessful(outcome);
                }
                else
                {
                    activity.SetFailed(outcome, outcome);
                }
            });
            TryObserve(() => SessionMetrics.Operations.Add(1,
                new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
            if (context is null)
            {
                TryObserve(() => SessionLog.OperationCompleted(_logger, operation, agentId, sessionId, outcome));
            }
            else
            {
                TryObserve(() => SessionLog.CorrelatedOperationCompleted(_logger, operation,
                    context.Identity.TenantId, context.AgentId, context.SessionId, context.ExecutionLaneId,
                    context.Correlation.OperationId, runId, turnId, outcome));
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryObserve(() => activity.SetFailed("cancelled", "cancellation"));
            RecordOperationMetric(operation, "cancelled");
            if (context is null)
            {
                TryObserve(() => SessionLog.OperationCancelled(_logger, operation, agentId, sessionId));
            }
            else
            {
                TryObserve(() => SessionLog.CorrelatedOperationCancelled(_logger, operation,
                    context.Identity.TenantId, context.AgentId, context.SessionId, context.ExecutionLaneId,
                    context.Correlation.OperationId, runId, turnId));
            }
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            TryObserve(() => activity.SetFailed("faulted", errorType));
            RecordOperationMetric(operation, "faulted");
            if (context is null)
            {
                TryObserve(() => SessionLog.OperationFaulted(_logger, operation, agentId, sessionId, errorType));
            }
            else
            {
                TryObserve(() => SessionLog.CorrelatedOperationFaulted(_logger, operation,
                    context.Identity.TenantId, context.AgentId, context.SessionId, context.ExecutionLaneId,
                    context.Correlation.OperationId, runId, turnId, errorType));
            }
            throw;
        }
    }

    private static string Outcome<TResult>(TResult result) where TResult : class => result switch
    {
        SessionCreated or SessionLoaded or SessionAppended or SessionPage or SessionBranched or SessionDeleted or
            SessionInputReplayFound or SessionInputNotFound or SessionExecutionLaneProvisioned or AcceptedInput or
            SessionRunAccepted or SessionRunStateLoaded =>
            "success",
        SessionCreateFailed or SessionNotFound or SessionLoadFailed or SessionAppendConflict or
            SessionAppendNotFound or SessionAppendFailed or SessionReadNotFound or SessionReadFailed or
            SessionBranchParentNotFound or SessionBranchFailed or SessionDeleteFailed or
            SessionInputLookupConflict or SessionInputLookupRejected or SessionExecutionLaneProvisionConflict or
            SessionExecutionLaneProvisionRejected or RejectedInput or SessionRunStartConflict or
            SessionRunStartBusy or SessionRunStartFenced or SessionRunStartRejected or SessionRunStateUnavailable =>
            "failed",
        _ => "unknown",
    };

    private static void RecordOperationMetric(string operation, string outcome) =>
        TryObserve(() => SessionMetrics.Operations.Add(1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));

    /// <summary>Publishes one post-commit semantic event to every sink without letting observation alter the committed result.</summary>
    /// <param name="eventFactory">Builds the event from the already committed store outcome.</param>
    /// <remarks>
    /// Publication deliberately ignores caller cancellation: the protected effect has already been committed, so a
    /// caller that stopped waiting must still see the event reach its sinks. Each sink receives
    /// <see cref="CancellationToken.None"/>, and construction or sink failures are swallowed.
    /// </remarks>
    private async ValueTask PublishBestEffortAsync(Func<SessionEvent> eventFactory)
    {
        Debug.Assert(eventFactory is not null, "A committed semantic-event factory is required.");
        SessionEvent sessionEvent;
        try
        {
            sessionEvent = eventFactory();
        }
        catch
        {
            // The protected effect is already committed; event construction is observational.
            return;
        }

        foreach (var sink in _eventSinks)
        {
            try
            {
                await sink.PublishAsync(sessionEvent, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // The protected effect is already committed; event observation cannot replace its result.
            }
        }
    }

    private static string SafeCreationLookupReason(SessionCreationLocationResult result) => result switch
    {
        SessionCreationLocationConflict conflict => conflict.SafeMessage,
        SessionDirectoryCreationLookupDenied denied => denied.SafeMessage,
        SessionDirectoryCreationLookupUnavailable unavailable => unavailable.SafeMessage,
        _ => "Session creation-route lookup returned an unsupported result.",
    };

    private static string SafeDirectoryWriteReason(SessionDirectoryWriteResult result) => result switch
    {
        SessionLocationConflict => "The session creation route conflicts with an existing route.",
        SessionDirectoryWriteDenied denied => denied.SafeMessage,
        SessionDirectoryWriteUnavailable unavailable => unavailable.SafeMessage,
        _ => "Session creation-route recording returned an unsupported result.",
    };

    private static string SafeLocationReason(SessionLocationResult result) => result switch
    {
        SessionLocationNotFound => "The session route is unavailable.",
        SessionDirectoryLookupDenied denied => denied.SafeMessage,
        SessionDirectoryLookupUnavailable unavailable => unavailable.SafeMessage,
        _ => "Session route lookup returned an unsupported result.",
    };

    private static string SafeSelectionReason(SessionStoreSelectionResult result) =>
        result is SessionStoreSelectionRejected rejected
            ? rejected.SafeMessage
            : "Session store selection returned an unsupported result.";

    private static void TryObserve(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try
        {
            observation();
        }
        catch
        {
            // Diagnostics cannot affect routing, authorization, or protected effects.
        }
    }
}
