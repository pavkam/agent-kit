// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Conformance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Composes the protected in-memory session store and supplies exact fresh authority for shared conformance cases.</summary>
public sealed class SqliteSessionStoreConformanceFixture:
    ISessionStoreConformanceFixture,
    ISecurityAuditDispatcher
{
    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
    private long _nextIdentity;
    private ServiceProvider? _services;
    private readonly string _directory = Path.Combine("/tmp", $"agentkit-session-{Guid.NewGuid():N}");
    private InMemorySecurityGrantStore? _grants;
    private SqliteSessionStore? _store;

    /// <inheritdoc/>
    public ValueTask<ISessionStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_services is null)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<TimeProvider>(_timeProvider);
            _ = Directory.CreateDirectory(_directory);
            _grants = new InMemorySecurityGrantStore(_timeProvider);
            _ = services.AddSingleton<ISecurityGrantStore>(_grants);
            _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
            _ = services.AddSqliteSessionStore(new SqliteSessionStoreTarget(
                Path.Combine(_directory, "sessions.db"),
                new SqliteSessionStoreInstanceId(Guid.NewGuid()),
                SqliteDatabaseOpenMode.CreateIfMissing,
                SqliteSchemaMode.ApplyKnownMigrations));
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            _store = (SqliteSessionStore) _services.GetRequiredService<ISessionStore>();
        }

        return ValueTask.FromResult<ISessionStore>(_store!);
    }

    /// <inheritdoc/>
    public async ValueTask<AuthorizedSessionStoreRequest<TRequest>> AuthorizeAsync<TRequest>(
        TRequest request, SecurityOperationKind kind, SecurityEffect effect,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (_store is null || _grants is null)
        {
            throw new InvalidOperationException("CreateAsync must compose the session store before authority is issued.");
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
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _services?.Dispose();
        return ValueTask.CompletedTask;
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
            _ => throw new InvalidOperationException($"Unsupported conformance request {typeof(TRequest).FullName}."),
        };
}
