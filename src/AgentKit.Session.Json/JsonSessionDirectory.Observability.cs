// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <content>Contains the protected public surface, exact grant enforcement, and content-free diagnostics for the JSON session directory.</content>
public sealed partial class JsonSessionDirectory
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The directory was disposed.</exception>
    /// <exception cref="InvalidOperationException">The directory was used before <see cref="InitializeAsync"/> completed.</exception>
    public ValueTask<SessionLocationResult> LocateAsync(
        AuthorizedSessionDirectoryRequest<SessionOperationContext> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync(AgentKitActivityNames.SessionDirectoryOperation, "locate",
            request.Request.AgentId, request.Request.SessionId,
            async token =>
            {
                RequireInitialized();
                var context = request.Request;
                var access = await AuthorizeAsync(
                    context.Authorization, context.Identity, request.Grant, request.Intent,
                    SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    SessionDirectorySecurityBinding.Resource(context.Identity.TenantId, context.ToAddress()),
                    SessionDirectorySecurityBinding.LocateFingerprint(context), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return access switch
                {
                    DirectoryAccessUnavailable unavailable =>
                        new SessionDirectoryLookupUnavailable(unavailable.SafeMessage),
                    DirectoryAccessDenied denied => new SessionDirectoryLookupDenied(denied.SafeMessage),
                    _ => LocateCore(context),
                };
            },
            static result => result is SessionLocated or SessionLocationNotFound, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The directory was disposed.</exception>
    /// <exception cref="InvalidOperationException">The directory was used before <see cref="InitializeAsync"/> completed.</exception>
    public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionCreateRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync(AgentKitActivityNames.SessionDirectoryOperation, "locate_for_create",
            request.Request.AgentId, null,
            async token =>
            {
                RequireInitialized();
                var create = request.Request;
                var access = await AuthorizeAsync(
                    create.Authorization, create.Identity, request.Grant, request.Intent,
                    SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    SessionDirectorySecurityBinding.CreationResource(
                        create.Identity.TenantId, create.AgentId, create.IdempotencyKey),
                    SessionDirectorySecurityBinding.LocateForCreateFingerprint(create), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return access switch
                {
                    DirectoryAccessUnavailable unavailable =>
                        new SessionDirectoryCreationLookupUnavailable(unavailable.SafeMessage),
                    DirectoryAccessDenied denied => new SessionDirectoryCreationLookupDenied(denied.SafeMessage),
                    _ => LocateForCreateCore(create),
                };
            },
            static result => result is SessionCreationLocationLocated or SessionCreationLocationNotFound,
            cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The directory was disposed.</exception>
    /// <exception cref="InvalidOperationException">The directory was used before <see cref="InitializeAsync"/> completed.</exception>
    /// <exception cref="IOException">The routing record could not be appended and flushed, in which case no route was committed.</exception>
    public ValueTask<SessionDirectoryWriteResult> RecordAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync(AgentKitActivityNames.SessionDirectoryOperation, "record",
            request.Request.Context.AgentId, request.Request.Context.SessionId,
            async token =>
            {
                RequireInitialized();
                var write = request.Request;
                var access = await AuthorizeAsync(
                    write.Context.Authorization, write.Context.Identity, request.Grant, request.Intent,
                    SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                    SessionDirectorySecurityBinding.Resource(
                        write.Context.Identity.TenantId, write.Location.Address),
                    SessionDirectorySecurityBinding.RecordFingerprint(write), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return access switch
                {
                    DirectoryAccessUnavailable unavailable =>
                        new SessionDirectoryWriteUnavailable(unavailable.SafeMessage),
                    DirectoryAccessDenied denied => new SessionDirectoryWriteDenied(denied.SafeMessage),
                    _ => RecordCore(write, token),
                };
            },
            static result => result is SessionLocationRecorded, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The directory was disposed.</exception>
    /// <exception cref="InvalidOperationException">The directory was used before <see cref="InitializeAsync"/> completed.</exception>
    /// <exception cref="IOException">The creation-route record could not be appended and flushed, in which case no route was committed.</exception>
    public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync(AgentKitActivityNames.SessionDirectoryOperation, "record_create",
            request.Request.Request.AgentId, request.Request.Location.Address.SessionId,
            async token =>
            {
                RequireInitialized();
                var record = request.Request;
                var create = record.Request;
                var access = await AuthorizeAsync(
                    create.Authorization, create.Identity, request.Grant, request.Intent,
                    SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                    SessionDirectorySecurityBinding.CreationResource(
                        create.Identity.TenantId, create.AgentId, create.IdempotencyKey),
                    SessionDirectorySecurityBinding.RecordCreateFingerprint(record), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return access switch
                {
                    DirectoryAccessUnavailable unavailable =>
                        new SessionDirectoryWriteUnavailable(unavailable.SafeMessage),
                    DirectoryAccessDenied denied => new SessionDirectoryWriteDenied(denied.SafeMessage),
                    _ => RecordCreateCore(record, token),
                };
            },
            static result => result is SessionLocationRecorded, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The directory was disposed.</exception>
    /// <exception cref="InvalidOperationException">The directory was used before <see cref="InitializeAsync"/> completed.</exception>
    /// <remarks>Discovery is bounded by the caller's page size and ordered by session identity, and it never crosses the tenant, agent, or owner boundary.</remarks>
    public ValueTask<SessionDirectoryListResult> ListAsync(
        AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveAsync(AgentKitActivityNames.SessionDirectoryList, "list", request.Request.AgentId, null,
            async token =>
            {
                RequireInitialized();
                var scan = request.Request;
                var access = await AuthorizeAsync(
                    scan.Authorization, scan.Identity, request.Grant, request.Intent,
                    SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    SessionDirectorySecurityBinding.ListResource(scan.Identity.TenantId, scan.AgentId),
                    SessionDirectorySecurityBinding.ListFingerprint(scan), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                SessionDirectoryListResult result = access switch
                {
                    DirectoryAccessUnavailable unavailable =>
                        new SessionDirectoryListUnavailable(unavailable.SafeMessage),
                    DirectoryAccessDenied denied => new SessionDirectoryListUnavailable(denied.SafeMessage),
                    _ => ListCore(scan),
                };
                return result;
            },
            static result => result is SessionDirectoryPage, cancellationToken);
    }

    /// <summary>Runs one directory operation under a correlated activity, a bounded counter, and content-free logs.</summary>
    /// <typeparam name="TResult">The typed terminal directory result.</typeparam>
    /// <param name="activityName">The shared AgentKit activity name for this boundary.</param>
    /// <param name="operation">The bounded directory operation name.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="sessionId">The session identity when already established.</param>
    /// <param name="action">The concrete directory implementation.</param>
    /// <param name="isSuccessful">The typed success classifier.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>The exact result produced by <paramref name="action"/>.</returns>
    /// <remarks>Activity, log, and meter failures are contained so observation can never replace the operation's result or exception.</remarks>
    private async ValueTask<TResult> ObserveAsync<TResult>(
        string activityName, string operation, AgentId agentId, SessionId? sessionId,
        Func<CancellationToken, ValueTask<TResult>> action, Func<TResult, bool> isSuccessful,
        CancellationToken cancellationToken)
        where TResult : class
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(activityName), "A shared activity name is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(action is not null, "A directory action is required.");
        Debug.Assert(isSuccessful is not null, "A result classifier is required.");
        using var scope = AgentKitActivityScope.Start(
            activityName,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.SessionOperation, operation },
                { AgentKitTagNames.AgentId, agentId.ToString() },
                { AgentKitTagNames.SessionId, sessionId?.ToString() },
            });
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action(cancellationToken).ConfigureAwait(false);
            var succeeded = isSuccessful(result);
            var outcome = succeeded ? "succeeded" : "rejected";
            Observe(() =>
            {
                if (succeeded)
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    scope.Activity.SetFailed(outcome, outcome);
                }
            });
            Observe(() => JsonSessionDirectoryLog.Completed(_logger, operation, agentId, sessionId, outcome));
            Observe(() => JsonSessionDirectoryMetrics.Record(operation, outcome));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Observe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            Observe(() => JsonSessionDirectoryLog.Completed(_logger, operation, agentId, sessionId, "cancelled"));
            Observe(() => JsonSessionDirectoryMetrics.Record(operation, "cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            Observe(() => scope.Activity.SetFailed("failed", errorType));
            Observe(() => JsonSessionDirectoryLog.Failed(_logger, operation, agentId, sessionId, errorType));
            Observe(() => JsonSessionDirectoryMetrics.Record(operation, "failed"));
            throw;
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
                CreateAuditRecord(authorization.Scope, grant, kind, effect, resource, fingerprint,
                    SecurityAuditEventKind.EnforcementProposed),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (audit is not SecurityAuditAccepted)
            {
                return new DirectoryAccessUnavailable(
                    "Required audit delivery is unavailable for the directory operation.");
            }

            var enforcement = new SecurityEnforcementRequest(
                authorization.Scope, identity, authorization, SecurityAudience, kind, effect, [resource], fingerprint,
                grant.RevocationVersion);
            var consumption = await _grantStore.ValidateAndConsumeAsync(
                grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsFreshExact(consumption, grant, enforcement, intent))
            {
                return new DirectoryAccessDenied("Directory authorization could not be verified.");
            }

            var intentAudit = await _auditDispatcher.DispatchAsync(
                CreateAuditRecord(authorization.Scope, grant, kind, effect, resource, fingerprint,
                    SecurityAuditEventKind.GrantConsumptionIntent),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return intentAudit is SecurityAuditAccepted
                ? new DirectoryAccessAllowed()
                : new DirectoryAccessUnavailable(
                    "Required audit delivery is unavailable for the directory operation.");
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

    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
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
                .Add("resource", RedactedAuditValue.FromFingerprint(
                    SessionStoreSecurityBinding.FingerprintResource(resource))),
            _timeProvider.GetUtcNow());
    }

    private static void Observe(Action observation)
    {
        Debug.Assert(observation is not null, "An observational delegate is required.");
        try
        {
            observation();
        }
        catch
        {
            // Observation failures never change durable session-directory behavior.
        }
    }
}
