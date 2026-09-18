// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Issues fresh single-use grants for an arbitrary <see cref="JsonSessionStore"/> composed by <see cref="TestStoreRoot"/>.</summary>
/// <remarks>
/// This mirrors <see cref="JsonSessionStoreConformanceFixture"/>'s own authorization logic, but is not bound to one fixture
/// instance, so it can authorize requests against a store that is reopened multiple times with different settings over the
/// same root.
/// </remarks>
internal sealed class TestStoreAuthority: ISecurityAuditDispatcher
{
    private readonly InMemorySecurityGrantStore _grants;
    private readonly TimeProvider _timeProvider;
    private long _nextIdentity;

    /// <summary>Initializes authority backed by the supplied grant store and clock.</summary>
    /// <param name="grants">The authoritative grant store the composed store also validates against.</param>
    /// <param name="timeProvider">The clock used to stamp issued grants.</param>
    internal TestStoreAuthority(InMemorySecurityGrantStore grants, TimeProvider timeProvider)
    {
        _grants = grants;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    /// <remarks>These cases exercise store semantics directly, so required audit always succeeds.</remarks>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <summary>Registers and returns a freshly issued single-use grant for the exact request.</summary>
    /// <typeparam name="TRequest">The immutable session request type.</typeparam>
    /// <param name="store">The store this authority issues authority for.</param>
    /// <param name="request">The complete request whose resource and fingerprint are bound.</param>
    /// <param name="kind">The exact protected operation kind.</param>
    /// <param name="effect">The exact protected effect.</param>
    /// <param name="cancellationToken">Cancels before authority is issued or registered.</param>
    internal async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
        JsonSessionStore store, TRequest request, SecurityOperationKind kind, SecurityEffect effect,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        var context = RequestContext(request);
        var resource = SessionStoreSecurityBinding.Resource(store.Descriptor.Key, context.ToAddress());
        var grant = new SecurityGrant(
            new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), context.Authorization.Scope,
            context.Identity, context.Authorization, store.SecurityAudience, kind, effect, [resource],
            RequestFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
            _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddDays(1), 1);
        await _grants.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        var fence = request is SessionRunStartRequest start ? start.ExpectedFencingToken : null;
        return new AuthorizedSessionStoreRequest<TRequest>(
            request, store.Descriptor.Key, grant, new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), fence));
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 3;
        return new Guid(bytes);
    }

    private static SessionOperationContext RequestContext<TRequest>(TRequest request)
        where TRequest : class => request switch
        {
            SessionStoreCreateRequest value => value.Context,
            SessionOperationContext value => value,
            SessionExecutionLaneProvisionRequest value => value.Context,
            SessionAppendRequest value => value.Context,
            SessionReadRequest value => value.Context,
            SessionBranchRequest value => value.Context,
            SessionDeleteRequest value => value.Context,
            SessionInputLookupRequest value => value.Context,
            SessionInputAdmissionRequest value => value.Context,
            SessionRunStartRequest value => value.Context,
            SessionRunStateRequest value => value.Context,
            SessionRunReleaseRequest value => value.Context,
            _ => throw new InvalidOperationException($"Unsupported conformance request {typeof(TRequest).FullName}."),
        };

    private static InputFingerprint RequestFingerprint<TRequest>(TRequest request)
        where TRequest : class => request switch
        {
            SessionStoreCreateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionOperationContext value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionExecutionLaneProvisionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionAppendRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionReadRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionBranchRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionDeleteRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionInputLookupRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionInputAdmissionRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionRunStartRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionRunStateRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            SessionRunReleaseRequest value => SessionStoreSecurityBinding.Fingerprint(value),
            _ => throw new InvalidOperationException($"Unsupported conformance request {typeof(TRequest).FullName}."),
        };
}
