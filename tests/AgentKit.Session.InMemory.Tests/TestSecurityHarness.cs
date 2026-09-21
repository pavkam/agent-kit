// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using System.Runtime.CompilerServices;

/// <summary>Issues deterministic exact grants for legacy store-behavior tests while retaining strict mismatch checks.</summary>
internal sealed class TestSecurityHarness
    : ISecurityGrantStore, ISecurityAuditDispatcher
{
    private static readonly ConditionalWeakTable<InMemorySessionStore, TestSecurityHarness> _stores = [];
    private readonly Dictionary<(TenantId Tenant, AgentId Agent, IdempotencyKey Key), SessionAddress> _creationAddresses = [];
    private readonly HashSet<GrantId> _consumed = [];
    private long _nextIdentity;

    /// <summary>Associates one test store with its security harness.</summary><param name="store">The non-null store.</param><param name="harness">The non-null harness.</param>
    internal static void Register(InMemorySessionStore store, TestSecurityHarness harness)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(harness);
        _stores.Add(store, harness);
    }

    /// <summary>Returns the harness associated with one test store.</summary><param name="store">The registered store.</param><returns>The per-store harness.</returns>
    internal static TestSecurityHarness For(InMemorySessionStore store) => _stores.GetValue(store, static _ =>
        throw new InvalidOperationException("The test store has no registered security harness."));

    /// <summary>Builds the stable addressed lower creation request for one logical creation key.</summary>
    internal SessionStoreCreateRequest Lower(SessionCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = (request.Identity.TenantId, request.AgentId, request.IdempotencyKey);
        if (!_creationAddresses.TryGetValue(key, out var address))
        {
            address = new SessionAddress(request.AgentId, new SessionId(NextGuid()));
            _creationAddresses.Add(key, address);
        }

        var authorization = CloneAuthorization(
            request.Authorization,
            new SecurityAuthorizationScope(request.AgentId, address.SessionId, request.Authorization.Scope.Correlation));
        var context = new SessionOperationContext(
            address.AgentId, address.SessionId, null, request.Authorization.Scope.Correlation, request.Identity, authorization);
        return new SessionStoreCreateRequest(request, address, context);
    }

    /// <summary>Wraps one exact request in a single-use matching grant.</summary>
    internal AuthorizedSessionStoreRequest<TRequest> Authorize<TRequest>(
        InMemorySessionStore store,
        TRequest request,
        SecurityOperationKind kind,
        SecurityEffect effect)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        var context = RequestContext(request);
        var resource = SessionStoreSecurityBinding.Resource(store.Descriptor.Key, context.ToAddress());
        var grant = new SecurityGrant(
            new GrantId(NextGuid()),
            new SecurityRequestId(NextGuid()),
            context.Authorization.Scope,
            context.Identity,
            context.Authorization,
            store.SecurityAudience,
            kind,
            effect,
            [resource],
            RequestFingerprint(request),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddYears(100),
            1);
        return new AuthorizedSessionStoreRequest<TRequest>(request, store.Descriptor.Key, grant,
            new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), null));
    }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant,
        SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = grant.Scope == enforcement.Scope
            && grant.Identity == enforcement.Identity
            && grant.Authorization == enforcement.Authorization
            && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind
            && grant.Effect == enforcement.Effect
            && grant.Resources.SequenceEqual(enforcement.Resources)
            && grant.InputFingerprint == enforcement.InputFingerprint
            && grant.RevocationVersion == enforcement.RevocationVersion;
        return !matches
            ? ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Mismatch, 0, "The exact grant binding did not match."))
            : ValueTask.FromResult(_consumed.Add(grant.Id)
            ? new GrantConsumptionResult(GrantConsumptionStatus.Consumed, 0, "The exact grant use was consumed.")
            : new GrantConsumptionResult(GrantConsumptionStatus.Exhausted, 0, "The grant use was already consumed."));
    }

    /// <inheritdoc/>
    public async ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant,
        SecurityEnforcementRequest enforcement, SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var legacy = await ValidateAndConsumeAsync(grant, enforcement, cancellationToken).ConfigureAwait(false);
        return legacy.Status != GrantConsumptionStatus.Consumed
            ? legacy
            : new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                legacy.RemainingUses,
                legacy.SafeMessage,
                new SecurityEnforcementIntentReceipt(
                    intent.Id, grant.Id, grant.RequestId, enforcement, intent.RequiredFence,
                    SecurityEnforcementBinding.Fingerprint(enforcement, intent), DateTimeOffset.UnixEpoch));
    }

    /// <inheritdoc/>
    public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 1;
        return new Guid(bytes);
    }

    private static SessionOperationContext RequestContext<TRequest>(TRequest request) where TRequest : class => request switch
    {
        SessionStoreCreateRequest value => value.Context,
        SessionOperationContext value => value,
        SessionAppendRequest value => value.Context,
        SessionReadRequest value => value.Context,
        SessionBranchRequest value => value.Context,
        SessionDeleteRequest value => value.Context,
        SessionExecutionLaneProvisionRequest value => value.Context,
        SessionInputLookupRequest value => value.Context,
        SessionInputAdmissionRequest value => value.Context,
        SessionRunStartRequest value => value.Context,
        SessionRunStateRequest value => value.Context,
        SessionRunReleaseRequest value => value.Context,
        SessionRunAbortRequest value => value.Context,
        _ => throw new InvalidOperationException($"Unsupported test request {typeof(TRequest).FullName}."),
    };

    private static InputFingerprint RequestFingerprint<TRequest>(TRequest request) where TRequest : class => request switch
    {
        SessionStoreCreateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionOperationContext value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionAppendRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionReadRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionBranchRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionDeleteRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionExecutionLaneProvisionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionInputLookupRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionInputAdmissionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunStartRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunStateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunReleaseRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        SessionRunAbortRequest value => SessionStoreSecurityBinding.Fingerprint(value),
        _ => throw new InvalidOperationException($"Unsupported test request {typeof(TRequest).FullName}."),
    };

    private static SecurityAuthorizationContext CloneAuthorization(
        SecurityAuthorizationContext source,
        SecurityAuthorizationScope scope) => new(
        source.ProfileKey, source.ProfileVersion, source.PolicySnapshot, source.AuthorityKey,
        source.AgentDefinitionRevision, source.ConfigurationVersion, scope, source.Identity);
}
