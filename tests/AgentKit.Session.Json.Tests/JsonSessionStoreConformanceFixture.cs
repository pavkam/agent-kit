// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using AgentKit.Conformance;

/// <summary>Composes the protected durable JSON session store and supplies exact fresh authority for shared conformance cases.</summary>
/// <remarks>
/// Each case owns one isolated initialized store root. The fixture registers a real in-memory grant store and an accepting
/// audit dispatcher as the store's actual enforcement dependencies, so every authorized request consumes a freshly
/// registered single-use grant and the store itself produces the authoritative intent receipt.
/// </remarks>
public sealed class JsonSessionStoreConformanceFixture: ISessionStoreConformanceFixture, ISecurityAuditDispatcher
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly string _directory = TestTemporaryDirectory.Create();
    private readonly JsonSessionStoreInstanceId _instanceId = new(Guid.NewGuid());
    private readonly JsonSessionStoreSettings _settings;
    private readonly JsonStoreRecoveryMode _recoveryMode;
    private long _nextIdentity;
    private ServiceProvider? _services;
    private InMemorySecurityGrantStore? _grants;
    private JsonSessionStore? _store;

    /// <summary>Initializes a fixture that composes the store under conservative default settings.</summary>
    public JsonSessionStoreConformanceFixture()
        : this(JsonSessionStoreSettings.CreateDefault(), JsonStoreRecoveryMode.RecoverTornAppends)
    {
    }

    /// <summary>Initializes a fixture that composes the store under caller-supplied settings and recovery policy.</summary>
    /// <param name="settings">The immutable bounds, continuation limits, compaction policy, and encoding contract.</param>
    /// <param name="recoveryMode">Whether an incomplete trailing append may be discarded during initialization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    public JsonSessionStoreConformanceFixture(JsonSessionStoreSettings settings, JsonStoreRecoveryMode recoveryMode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _recoveryMode = recoveryMode;
    }

    /// <summary>Gets the fully qualified store root this fixture composes, for direct filesystem manipulation in tests.</summary>
    public string DirectoryPath => Path.Combine(_directory, "sessions");

    /// <inheritdoc/>
    public async ValueTask<ISessionStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_store is null)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<TimeProvider>(_timeProvider);
            _grants = new InMemorySecurityGrantStore(_timeProvider);
            _ = services.AddSingleton<ISecurityGrantStore>(_grants);
            _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
            _ = services.AddJsonSessionStore(
                new JsonSessionStoreTarget(DirectoryPath, _instanceId, JsonStoreOpenMode.CreateIfMissing, _recoveryMode),
                _settings);
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            _store = _services.GetRequiredService<ISessionStore>().ShouldBeOfType<JsonSessionStore>();
            await _store.InitializeAsync(cancellationToken);
        }

        return _store;
    }

    /// <summary>Disposes the composed store and reopens the identical root, as a process restart would.</summary>
    /// <param name="cancellationToken">Cancels before the reopened store finishes replaying its log.</param>
    /// <returns>A freshly composed and initialized store bound to the same root and persistent identity.</returns>
    /// <remarks>
    /// Disposal releases the root's advisory exclusive lock and drops the whole projection, so the returned store can only
    /// know what the durable record log told it. Grants are single use and are reissued per call, so a new grant store is
    /// composed with the reopened store.
    /// </remarks>
    public async ValueTask<JsonSessionStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _services = null;
        _store = null;
        _grants = null;
        _ = await CreateAsync(cancellationToken);
        return _store!;
    }

    /// <inheritdoc/>
    public async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
        TRequest request,
        SecurityOperationKind kind,
        SecurityEffect effect,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_store is null || _grants is null)
        {
            throw new InvalidOperationException(
                "CreateAsync must compose the session store before authority is issued.");
        }

        var context = RequestContext(request);
        var resource = SessionStoreSecurityBinding.Resource(_store.Descriptor.Key, context.ToAddress());
        var grant = new SecurityGrant(
            new GrantId(NextGuid()), new SecurityRequestId(NextGuid()), context.Authorization.Scope,
            context.Identity, context.Authorization, _store.SecurityAudience, kind, effect, [resource],
            RequestFingerprint(request), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
            _timeProvider.GetUtcNow(), _timeProvider.GetUtcNow().AddDays(1), 1);
        await _grants.RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        var fence = request is SessionRunStartRequest start ? start.ExpectedFencingToken : null;
        return new AuthorizedSessionStoreRequest<TRequest>(
            request, _store.Descriptor.Key, grant,
            new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), fence));
    }

    /// <inheritdoc/>
    /// <remarks>The conformance suite exercises store semantics, so required audit always succeeds here.</remarks>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 1;
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
