// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

public sealed partial class SqliteSessionStore
{
    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.session.sqlite");

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(AuthorizedSessionStoreRequest<SessionStoreCreateRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
            (uow, token) => CreateCoreAsync(uow, request.Request, token), static result => result is SessionCreated,
            static reason => new SessionCreateFailed(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(AuthorizedSessionStoreRequest<SessionOperationContext> context, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(context, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            (uow, token) => LoadCoreAsync(uow, context.Request, token), static result => result is SessionLoaded,
            static reason => new SessionLoadFailed(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(AuthorizedSessionStoreRequest<SessionExecutionLaneProvisionRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
            (uow, token) => ProvisionLaneCoreAsync(uow, request.Request, token), static result => result is SessionExecutionLaneProvisioned,
            static reason => new SessionExecutionLaneProvisionRejected(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(AuthorizedSessionStoreRequest<SessionLaneStateRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            (uow, token) => LoadLaneStateCoreAsync(uow, request.Request, token),
            static result => result is SessionLaneStateLoaded or SessionLaneStateNotProvisioned,
            static reason => new SessionLaneStateUnavailable(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionAppendResult> AppendAsync(AuthorizedSessionStoreRequest<SessionAppendRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append,
            (uow, token) => AppendCoreAsync(uow, request.Request, token), static result => result is SessionAppended,
            static reason => new SessionAppendFailed(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(AuthorizedSessionStoreRequest<SessionReadRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            (uow, token) => ReadCoreAsync(uow, request.Request, token), static result => result is SessionPage,
            static reason => new SessionReadFailed(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> CreateBranchAsync(AuthorizedSessionStoreRequest<SessionBranchRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Create,
            (uow, token) => CreateBranchCoreAsync(uow, request.Request, token), static result => result is SessionBranched,
            static reason => new SessionBranchFailed(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(AuthorizedSessionStoreRequest<SessionDeleteRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Delete,
            (uow, token) => DeleteCoreAsync(uow, request.Request, token), static result => result is SessionDeleted,
            static reason => new SessionDeleteFailed(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionInputLookupResult> LookupInputAsync(AuthorizedSessionStoreRequest<SessionInputLookupRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            (uow, token) => LookupInputCoreAsync(uow, request.Request, token), static result => result is SessionInputReplayFound or SessionInputNotFound,
            static reason => new SessionInputLookupRejected(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<InputAdmissionResult> AdmitInputAsync(AuthorizedSessionStoreRequest<SessionInputAdmissionRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Append,
            (uow, token) => AdmitInputCoreAsync(uow, request.Request, token), static result => result is AcceptedInput,
            static reason => new RejectedInput(new InputRejection(InputRejectionKind.Unauthorized, reason)), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionRunStartResult> AcceptRunAsync(AuthorizedSessionStoreRequest<SessionRunStartRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            (uow, token) => AcceptRunCoreAsync(uow, request.Request, token), static result => result is SessionRunAccepted,
            static reason => new SessionRunStartRejected(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(AuthorizedSessionStoreRequest<SessionRunStateRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            (uow, token) => LoadRunStateCoreAsync(uow, request.Request, token), static result => result is SessionRunStateLoaded,
            static reason => new SessionRunStateUnavailable(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(AuthorizedSessionStoreRequest<SessionRunReleaseRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            (uow, token) => ReleaseRunCoreAsync(uow, request.Request, token), static result => result is SessionRunReleased,
            static reason => new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Unsupported, reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionPendingInputsResult> LoadPendingInputsAsync(AuthorizedSessionStoreRequest<SessionPendingInputsRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateRead, SecurityEffect.Observe,
            (uow, token) => LoadPendingInputsCoreAsync(uow, request.Request, token), static result => result is SessionPendingInputsLoaded,
            static reason => new SessionPendingInputsUnavailable(reason), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<SessionInputPromotionResult> PromoteInputAsync(AuthorizedSessionStoreRequest<SessionInputPromotionRequest> request, CancellationToken cancellationToken = default) =>
        ObserveAuthorizedAsync(request, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            (uow, token) => PromoteInputCoreAsync(uow, request.Request, token), static result => result is SessionInputPromoted,
            static reason => new SessionInputPromotionRejected(reason), cancellationToken);

    private ValueTask<TResult> ObserveAuthorizedAsync<TRequest, TResult>(
        AuthorizedSessionStoreRequest<TRequest> request, SecurityOperationKind kind, SecurityEffect effect,
        Func<SqliteSessionUnitOfWork, CancellationToken, ValueTask<TResult>> action, Func<TResult, bool> succeeded,
        Func<string, TResult> rejected, CancellationToken cancellationToken)
        where TRequest : class where TResult : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Debug.Assert(action is not null, "A concrete session-store action is required.");
        Debug.Assert(succeeded is not null, "A bounded success classifier is required.");
        Debug.Assert(rejected is not null, "An authorization rejection factory is required.");
        return SessionStoreObservability.ObserveAsync(
            _logger, OperationName(request.Request), AgentId(request.Request), SessionId(request.Request),
            async token =>
            {
                var denial = await EnforceAsync(request, kind, effect, token).ConfigureAwait(false);
                return denial is not null
                    ? rejected(denial)
                    : kind == SecurityOperationKind.StateRead
                        ? await _database.RunReadAsync(action, token).ConfigureAwait(false)
                        : await _database.RunWriteAsync(action, token).ConfigureAwait(false);
            }, succeeded, cancellationToken);
    }

    private async ValueTask<string?> EnforceAsync<TRequest>(AuthorizedSessionStoreRequest<TRequest> wrapper,
        SecurityOperationKind kind, SecurityEffect effect, CancellationToken cancellationToken) where TRequest : class
    {
        Debug.Assert(wrapper is not null, "A protected request is required.");
        if (wrapper.StoreKey != Descriptor.Key)
        {
            return "The grant targets a different session store.";
        }

        var requestedFence = wrapper.Request is SessionRunStartRequest start
            ? start.ExpectedFencingToken
            : null;
        if (wrapper.Intent.RequiredFence != requestedFence)
        {
            return "The enforcement intent fence differs from the session request.";
        }
        if (!Descriptor.SupportsDistributedFencing && wrapper.Intent.RequiredFence is not null)
        {
            return "The selected session store does not support distributed fencing.";
        }

        SecurityEnforcementRequest enforcement;
        if (wrapper.Request is SessionStoreCreateRequest create)
        {
            var context = create.Context;
            enforcement = new SecurityEnforcementRequest(
                context.Authorization.Scope, context.Identity, context.Authorization, SecurityAudience, kind, effect,
                [SessionStoreSecurityBinding.Resource(Descriptor.Key, create.Address)],
                SessionStoreSecurityBinding.Fingerprint(create), wrapper.Grant.RevocationVersion);
        }
        else
        {
            var context = Context(wrapper.Request);
            enforcement = new SecurityEnforcementRequest(
                context.Authorization.Scope, context.Identity, context.Authorization, SecurityAudience, kind, effect,
                [SessionStoreSecurityBinding.Resource(Descriptor.Key, context.ToAddress())],
                Fingerprint(wrapper.Request), wrapper.Grant.RevocationVersion);
        }

        try
        {
            var consumption = await _grants.ValidateAndConsumeAsync(
                wrapper.Grant, enforcement, wrapper.Intent, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (consumption.Status != GrantConsumptionStatus.Consumed)
            {
                return consumption.SafeMessage;
            }
            if (consumption.IntentReceipt is not { } receipt
                || !ReceiptMatches(receipt, wrapper, enforcement))
            {
                return "The grant store returned invalid enforcement-intent evidence.";
            }

            var audit = await _auditDispatcher.DispatchAsync(
                CreateAuditRecord(wrapper.Grant, enforcement), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return audit is SecurityAuditAccepted
                ? null
                : "Required audit delivery is unavailable for the session-store operation.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return "A session-store authorization prerequisite is unavailable.";
        }
    }

    private static bool ReceiptMatches<TRequest>(
        SecurityEnforcementIntentReceipt receipt,
        AuthorizedSessionStoreRequest<TRequest> wrapper,
        SecurityEnforcementRequest enforcement)
        where TRequest : class
    {
        Debug.Assert(receipt is not null, "A grant-store intent receipt is required.");
        Debug.Assert(wrapper is not null, "A protected session-store request is required.");
        Debug.Assert(enforcement is not null, "Recomputed session-store enforcement evidence is required.");
        return receipt.IntentId == wrapper.Intent.Id
            && receipt.GrantId == wrapper.Grant.Id
            && receipt.RequestId == wrapper.Grant.RequestId
            && receipt.RequiredFence == wrapper.Intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, wrapper.Intent)
            && EnforcementMatches(receipt.Enforcement, enforcement);
    }

    private static bool EnforcementMatches(
        SecurityEnforcementRequest actual,
        SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "Retained enforcement evidence is required.");
        Debug.Assert(expected is not null, "Recomputed enforcement evidence is required.");
        return actual.Scope == expected.Scope
            && actual.Identity == expected.Identity
            && actual.Authorization == expected.Authorization
            && actual.Audience == expected.Audience
            && actual.Kind == expected.Kind
            && actual.Effect == expected.Effect
            && actual.InputFingerprint == expected.InputFingerprint
            && actual.RevocationVersion == expected.RevocationVersion
            && actual.Resources.SequenceEqual(expected.Resources);
    }

    private SecurityAuditRecord CreateAuditRecord(SecurityGrant grant, SecurityEnforcementRequest enforcement)
    {
        Debug.Assert(grant is not null, "A consumed grant is required for audit.");
        Debug.Assert(enforcement is not null, "Recomputed enforcement evidence is required for audit.");
        var resourceFingerprint = SessionStoreSecurityBinding.FingerprintResource(enforcement.Resources[0]);
        return new SecurityAuditRecord(
            _auditRecordIds.Create(),
            enforcement.Scope,
            grant.RequestId,
            grant.Id,
            null,
            SecurityAuditEventKind.GrantConsumptionIntent,
            SecurityAuditOutcome.Accepted,
            grant.PolicyVersion,
            ImmutableDictionary<string, RedactedAuditValue>.Empty
                .Add("audience", RedactedAuditValue.FromComponentId(enforcement.Audience))
                .Add("effect", RedactedAuditValue.FromEffect(enforcement.Effect))
                .Add("fingerprint", RedactedAuditValue.FromFingerprint(new ContentHash(enforcement.InputFingerprint.Value)))
                .Add("kind", RedactedAuditValue.FromOperationKind(enforcement.Kind))
                .Add("resource", RedactedAuditValue.FromFingerprint(resourceFingerprint)),
            _timeProvider.GetUtcNow());
    }

    private static string OperationName<TRequest>(TRequest request) where TRequest : class => request switch
    {
        SessionStoreCreateRequest => "create",
        SessionOperationContext => "load",
        SessionExecutionLaneProvisionRequest => "lane_provision",
        SessionLaneStateRequest => "lane_state_load",
        SessionAppendRequest => "append",
        SessionReadRequest => "read",
        SessionBranchRequest => "branch",
        SessionDeleteRequest => "delete",
        SessionInputLookupRequest => "input_lookup",
        SessionInputAdmissionRequest => "input_admit",
        SessionRunStartRequest => "run_accept",
        SessionRunStateRequest => "run_state_load",
        SessionRunReleaseRequest => "run_release",
        SessionPendingInputsRequest => "pending_inputs_load",
        SessionInputPromotionRequest => "input_promote",
        _ => "unknown",
    };

    private static AgentId AgentId<TRequest>(TRequest request) where TRequest : class =>
        request is SessionStoreCreateRequest create ? create.Address.AgentId : Context(request).AgentId;

    private static SessionId? SessionId<TRequest>(TRequest request) where TRequest : class =>
        request is SessionStoreCreateRequest create ? create.Address.SessionId : Context(request).SessionId;

    private static SessionOperationContext Context<TRequest>(TRequest request) where TRequest : class => request switch
    {
        SessionOperationContext value => value,
        SessionExecutionLaneProvisionRequest value => value.Context,
        SessionLaneStateRequest value => value.Context,
        SessionAppendRequest value => value.Context,
        SessionReadRequest value => value.Context,
        SessionBranchRequest value => value.Context,
        SessionDeleteRequest value => value.Context,
        SessionInputLookupRequest value => value.Context,
        SessionInputAdmissionRequest value => value.Context,
        SessionRunStartRequest value => value.Context,
        SessionRunStateRequest value => value.Context,
        SessionRunReleaseRequest value => value.Context,
        SessionPendingInputsRequest value => value.Context,
        SessionInputPromotionRequest value => value.Context,
        _ => throw new InvalidOperationException($"Unsupported session request type {typeof(TRequest).FullName}."),
    };

    private static InputFingerprint Fingerprint<TRequest>(TRequest request) where TRequest : class => request switch
    {
        SessionOperationContext value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionExecutionLaneProvisionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionLaneStateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionAppendRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionReadRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionBranchRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionDeleteRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionInputLookupRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionInputAdmissionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunStartRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunStateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunReleaseRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionPendingInputsRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionInputPromotionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        _ => throw new InvalidOperationException($"Unsupported session request type {typeof(TRequest).FullName}."),
    };
}
