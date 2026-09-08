// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Provides a process-local, tenant-masking, protected session-directory implementation.</summary>
/// <remarks>The directory is intentionally non-durable and is suitable only where the selected session profile permits process-local routing. Every lookup and mutation requires required-audit acceptance followed by exact grant consumption before directory state is read or changed. It never probes stores or rebinds a pinned location.</remarks>
public sealed class InMemorySessionDirectory: ISessionDirectory
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SessionAddress, SessionLocation> _locations = [];
    private readonly Dictionary<CreationRouteKey, CreationRoute> _creationRoutes = [];
    private readonly Dictionary<DirectoryWriteKey, DirectoryWriteRoute> _writeRoutes = [];
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly ISecurityGrantStore _grantStore;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemorySessionDirectory> _logger;

    /// <summary>Initializes a protected local directory with explicit security collaborators.</summary>
    /// <param name="securityAudience">The non-default component identity permitted to consume directory grants.</param>
    /// <param name="auditDispatcher">The non-null dispatcher that must accept required audit before access.</param>
    /// <param name="grantStore">The non-null authoritative grant store that validates and consumes one exact directory use.</param>
    /// <param name="auditRecordIds">The non-null deterministic source for audit-record identities.</param>
    /// <param name="timeProvider">The non-null clock used only for audit timestamps.</param>
    /// <param name="logger">The optional logger for content-free directory diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
    /// <exception cref="ArgumentNullException">Any collaborator is null.</exception>
    public InMemorySessionDirectory(
        ComponentId securityAudience,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grantStore,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        ILogger<InMemorySessionDirectory>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        SecurityAudience = securityAudience;
        _auditDispatcher = auditDispatcher;
        _grantStore = grantStore;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<InMemorySessionDirectory>.Instance;
    }

    /// <inheritdoc/>
    public bool Durable => false;

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; }

    /// <inheritdoc/>
    public ValueTask<SessionLocationResult> LocateAsync(
        AuthorizedSessionDirectoryRequest<SessionOperationContext> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionDirectoryObservability.ObserveAsync(_logger, "locate", request.Request.AgentId,
            request.Request.SessionId, token => LocateCoreAsync(request, token),
            static result => result is SessionLocated or SessionLocationNotFound, cancellationToken);
    }

    private async ValueTask<SessionLocationResult> LocateCoreAsync(
        AuthorizedSessionDirectoryRequest<SessionOperationContext> request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public locate boundary validates the request.");
        var context = request.Request;
        var resource = SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress());
        var authorization = await AuthorizeAsync(
            context.Authorization,
            context.Identity,
            request.Grant,
            request.Intent,
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            resource,
            SessionDirectorySecurityBinding.LocateFingerprint(context),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is DirectoryAccessUnavailable unavailable)
        {
            return new SessionDirectoryLookupUnavailable(unavailable.SafeMessage);
        }

        if (authorization is DirectoryAccessDenied denied)
        {
            return new SessionDirectoryLookupDenied(denied.SafeMessage);
        }

        using (_gate.EnterScope())
        {
            return !_locations.TryGetValue(context.ToAddress(), out var location)
                || location.TenantId != context.Identity.TenantId
                ? new SessionLocationNotFound(context.ToAddress())
                : new SessionLocated(location);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionCreateRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionDirectoryObservability.ObserveAsync(_logger, "locate_for_create", request.Request.AgentId,
            null, token => LocateForCreateCoreAsync(request, token),
            static result => result is SessionCreationLocationLocated or SessionCreationLocationNotFound,
            cancellationToken);
    }

    private async ValueTask<SessionCreationLocationResult> LocateForCreateCoreAsync(
        AuthorizedSessionDirectoryRequest<SessionCreateRequest> request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public create-lookup boundary validates the request.");
        var create = request.Request;
        var resource = SessionDirectorySecurityBinding.CreationResource(
            create.Identity.TenantId,
            create.AgentId,
            create.IdempotencyKey);
        var authorization = await AuthorizeAsync(
            create.Authorization,
            create.Identity,
            request.Grant,
            request.Intent,
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            resource,
            SessionDirectorySecurityBinding.LocateForCreateFingerprint(create),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is DirectoryAccessUnavailable unavailable)
        {
            return new SessionDirectoryCreationLookupUnavailable(unavailable.SafeMessage);
        }

        if (authorization is DirectoryAccessDenied denied)
        {
            return new SessionDirectoryCreationLookupDenied(denied.SafeMessage);
        }

        using (_gate.EnterScope())
        {
            var key = new CreationRouteKey(create.Identity.TenantId, create.AgentId, create.IdempotencyKey);
            return _creationRoutes.TryGetValue(key, out var route)
                ? route.Request.Equals(create)
                    ? new SessionCreationLocationLocated(route.Location)
                    : new SessionCreationLocationConflict("The creation retry key was already used with different request evidence.")
                : new SessionCreationLocationNotFound();
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryWriteResult> RecordAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionDirectoryObservability.ObserveAsync(_logger, "record", request.Request.Context.AgentId,
            request.Request.Context.SessionId, token => RecordCoreAsync(request, token),
            static result => result is SessionLocationRecorded, cancellationToken);
    }

    private async ValueTask<SessionDirectoryWriteResult> RecordCoreAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public record boundary validates the request.");
        var write = request.Request;
        var resource = SessionDirectorySecurityBinding.Resource(write.Context.Identity.TenantId, write.Location.Address);
        var authorization = await AuthorizeAsync(
            write.Context.Authorization,
            write.Context.Identity,
            request.Grant,
            request.Intent,
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            resource,
            SessionDirectorySecurityBinding.RecordFingerprint(write),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is DirectoryAccessUnavailable unavailable)
        {
            return new SessionDirectoryWriteUnavailable(unavailable.SafeMessage);
        }

        if (authorization is DirectoryAccessDenied denied)
        {
            return new SessionDirectoryWriteDenied(denied.SafeMessage);
        }

        using (_gate.EnterScope())
        {
            var routeKey = new DirectoryWriteKey(
                write.Context.Identity.TenantId,
                write.Context.ToAddress(),
                write.IdempotencyKey);
            if (_writeRoutes.TryGetValue(routeKey, out var previousWrite))
            {
                return previousWrite.Request.Equals(write)
                    ? new SessionLocationRecorded(previousWrite.Location, existing: true)
                    : new SessionLocationConflict(previousWrite.Location, write.Location.StoreKey);
            }

            if (!_locations.TryGetValue(write.Location.Address, out var existing))
            {
                _locations.Add(write.Location.Address, write.Location);
                _writeRoutes.Add(routeKey, new DirectoryWriteRoute(write, write.Location));
                return new SessionLocationRecorded(write.Location, existing: false);
            }

            if (existing.TenantId != write.Context.Identity.TenantId)
            {
                return new SessionDirectoryWriteDenied("The directory route cannot be recorded.");
            }
            if (existing.StoreKey != write.Location.StoreKey)
            {
                return new SessionLocationConflict(existing, write.Location.StoreKey);
            }

            _writeRoutes.Add(routeKey, new DirectoryWriteRoute(write, existing));
            return new SessionLocationRecorded(existing, existing: true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SessionDirectoryObservability.ObserveAsync(_logger, "record_create", request.Request.Request.AgentId,
            request.Request.Location.Address.SessionId, token => RecordCreateCoreAsync(request, token),
            static result => result is SessionLocationRecorded, cancellationToken);
    }

    private async ValueTask<SessionDirectoryWriteResult> RecordCreateCoreAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "The public create-record boundary validates the request.");
        var record = request.Request;
        var create = record.Request;
        var resource = SessionDirectorySecurityBinding.CreationResource(
            create.Identity.TenantId,
            create.AgentId,
            create.IdempotencyKey);
        var authorization = await AuthorizeAsync(
            create.Authorization,
            create.Identity,
            request.Grant,
            request.Intent,
            SecurityOperationKind.StateMutation,
            SecurityEffect.Mutate,
            resource,
            SessionDirectorySecurityBinding.RecordCreateFingerprint(record),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (authorization is DirectoryAccessUnavailable unavailable)
        {
            return new SessionDirectoryWriteUnavailable(unavailable.SafeMessage);
        }

        if (authorization is DirectoryAccessDenied denied)
        {
            return new SessionDirectoryWriteDenied(denied.SafeMessage);
        }

        using (_gate.EnterScope())
        {
            var routeKey = new CreationRouteKey(create.Identity.TenantId, create.AgentId, create.IdempotencyKey);
            if (_creationRoutes.TryGetValue(routeKey, out var existingRoute))
            {
                return existingRoute.Request.Equals(create)
                    ? new SessionLocationRecorded(existingRoute.Location, existing: true)
                    : new SessionLocationConflict(existingRoute.Location, record.Location.StoreKey);
            }

            if (_locations.TryGetValue(record.Location.Address, out var existingLocation))
            {
                return existingLocation.TenantId != create.Identity.TenantId
                    ? new SessionDirectoryWriteDenied("The directory route cannot be recorded.")
                    : new SessionLocationConflict(existingLocation, record.Location.StoreKey);
            }

            _locations.Add(record.Location.Address, record.Location);
            _creationRoutes.Add(routeKey, new CreationRoute(create, record.Location));
            return new SessionLocationRecorded(record.Location, existing: false);
        }
    }

    private async ValueTask<DirectoryAccessResult> AuthorizeAsync(
        SecurityAuthorizationContext authorization,
        ExecutionIdentity identity,
        SecurityGrant grant,
        SecurityEnforcementIntent intent,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ProtectedResource resource,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        Debug.Assert(authorization is not null, "Public directory requests provide captured authorization.");
        Debug.Assert(identity is not null, "Public directory requests provide execution identity.");
        Debug.Assert(grant is not null, "Authorized directory requests provide a grant.");
        try
        {
            var audit = await _auditDispatcher.DispatchAsync(
                CreateAuditRecord(authorization.Scope, grant, kind, effect, resource, fingerprint, SecurityAuditEventKind.EnforcementProposed),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (audit is not SecurityAuditAccepted)
            {
                return new DirectoryAccessUnavailable("Required audit delivery is unavailable for the directory operation.");
            }

            var enforcement = new SecurityEnforcementRequest(
                authorization.Scope,
                identity,
                authorization,
                SecurityAudience,
                kind,
                effect,
                [resource],
                fingerprint,
                grant.RevocationVersion);
            var consumption = await _grantStore.ValidateAndConsumeAsync(
                grant,
                enforcement,
                intent,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsFreshExact(consumption, grant, enforcement, intent))
            {
                return new DirectoryAccessDenied("Directory authorization could not be verified.");
            }
            var intentAudit = await _auditDispatcher.DispatchAsync(
                CreateAuditRecord(authorization.Scope, grant, kind, effect, resource, fingerprint, SecurityAuditEventKind.GrantConsumptionIntent), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return intentAudit is SecurityAuditAccepted ? new DirectoryAccessAllowed() : new DirectoryAccessUnavailable("Required audit delivery is unavailable for the directory operation.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new DirectoryAccessUnavailable("The directory authorization prerequisite is unavailable.");
        }
    }

    private static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        Debug.Assert(consumption is not null, "The grant store returns a non-null consumption result.");
        Debug.Assert(grant is not null, "The directory received a non-null grant.");
        Debug.Assert(enforcement is not null, "The directory created concrete enforcement evidence.");
        Debug.Assert(intent is not null, "The directory received a non-null enforcement intent.");
        return consumption.Status is GrantConsumptionStatus.Consumed
            && consumption.IntentReceipt is { } receipt
            && receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
            && HasExactEnforcement(receipt.Enforcement, enforcement);
    }

    private static bool HasExactEnforcement(
        SecurityEnforcementRequest actual,
        SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt retains non-null enforcement evidence.");
        Debug.Assert(expected is not null, "The directory created non-null enforcement evidence.");
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

    private SecurityAuditRecord CreateAuditRecord(
        SecurityAuthorizationScope scope,
        SecurityGrant grant,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ProtectedResource resource,
        InputFingerprint fingerprint,
        SecurityAuditEventKind eventKind)
    {
        Debug.Assert(scope is not null, "Authorization scope is required for audit.");
        Debug.Assert(grant is not null, "Grant evidence is required for audit.");
        return new SecurityAuditRecord(
            _auditRecordIds.Create(),
            scope,
            grant.RequestId,
            grant.Id,
            null,
            eventKind,
            SecurityAuditOutcome.Accepted,
            grant.PolicyVersion,
            ImmutableDictionary<string, RedactedAuditValue>.Empty
                .Add("audience", RedactedAuditValue.FromComponentId(SecurityAudience))
                .Add("effect", RedactedAuditValue.FromEffect(effect))
                .Add("fingerprint", RedactedAuditValue.FromFingerprint(new ContentHash(fingerprint.Value)))
                .Add("kind", RedactedAuditValue.FromOperationKind(kind))
                .Add("resource", RedactedAuditValue.FromFingerprint(SessionStoreSecurityBinding.FingerprintResource(resource))),
            _timeProvider.GetUtcNow());
    }

}
