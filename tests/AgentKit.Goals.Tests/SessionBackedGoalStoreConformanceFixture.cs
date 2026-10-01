// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using AgentKit.Session;
using AgentKit.Session.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Composes a <see cref="SessionBackedGoalStore"/> over the in-memory session store for the shared goal-store suite.</summary>
/// <remarks>Session coordination is authorized by a stand-in authority registered with the real in-memory grant store, exactly as the session-backed input queue suite does; goal grants are checked by the goal test harness.</remarks>
public sealed class SessionBackedGoalStoreConformanceFixture:
    IGoalStoreConformanceFixture,
    ISecurityProfileSelector,
    ISecurityAuthoritySelector,
    ISecurityAuthority,
    ISecurityAuditDispatcher,
    IDisposable
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UnixEpoch);
    private readonly ServiceProvider _services;
    private readonly ISessionCoordinator _coordinator;
    private readonly SessionProfileSnapshot _profile = TestSecurityEvidence.SessionProfile("agentkit.in-memory");
    private long _next;

    /// <summary>Initializes the isolated composition.</summary>
    public SessionBackedGoalStoreConformanceFixture()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton<TimeProvider>(_clock);
        _ = services.AddAgentPermissions(static options => options.PolicySnapshot = TestSecurityEvidence.PolicySnapshot);
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionDirectory(new ComponentId("conformance-directory"));
        _ = services.RemoveAll<ISecurityAuditDispatcher>();
        _ = services.AddSingleton<ISecurityAuditDispatcher>(this);
        _ = services.RemoveAll<ISecurityProfileSelector>();
        _ = services.AddSingleton<ISecurityProfileSelector>(this);
        _ = services.RemoveAll<ISecurityAuthoritySelector>();
        _ = services.AddSingleton<ISecurityAuthoritySelector>(this);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _coordinator = _services.GetRequiredService<ISessionCoordinator>();
        Store = NewStore();
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public bool PreservesDelegation => false;

    /// <inheritdoc/>
    public ComponentId? IntentScanner => null;

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IGoalStore Store { get; }

    /// <inheritdoc/>
    public async ValueTask<GoalConformanceOwner> CreateOwnerAsync(string tenant, CancellationToken cancellationToken = default)
    {
        var identity = GoalTestData.Identity(tenant);
        var agent = new AgentId(NextGuid());
        var creation = new BeforeRunOperationCorrelation(new OperationId(NextGuid()), null);
        var created = (await _coordinator.CreateAsync(
            new SessionCreateRequest(
                agent, identity, TestSecurityEvidence.Authorization(agent, null, creation, identity),
                conversationId: null, new IdempotencyKey($"goal-owner-{NextGuid()}"), ExtensionData.Empty),
            _profile,
            cancellationToken).ConfigureAwait(false)).ShouldBeOfType<SessionCreated>().Descriptor;
        var run = new RunId(NextGuid());
        var authorization = TestSecurityEvidence.Authorization(
            agent, created.Address.SessionId, new InRunOperationCorrelation(new OperationId(NextGuid()), run, null), identity);
        return new GoalConformanceOwner(agent, created.Address.SessionId, run, identity, authorization);
    }

    /// <inheritdoc/>
    public ValueTask<IGoalStore> ReopenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IGoalStore>(NewStore());

    /// <inheritdoc/>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
            TestSecurityEvidence.Authorization(request.Scope.AgentId, request.Scope.SessionId, request.Scope.Correlation, request.Identity)));
    }

    /// <inheritdoc/>
    ValueTask<SecurityAuthoritySelectionResult> ISecurityAuthoritySelector.SelectAsync(
        SecurityAuthorizationContext authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthoritySelectionResult>(new SecurityAuthoritySelected(authorization, this));
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = request.Authorization
            ?? throw new InvalidOperationException("Session coordinator requests retain captured authorization.");
        var grant = new SecurityGrant(
            new GrantId(NextGuid()), request.Id, request.Scope, request.Identity, authorization, request.Audience,
            request.Kind, request.Effect, request.Resources, request.InputFingerprint,
            authorization.PolicySnapshot.Version, new SecurityRevocationVersion(1), _clock.GetUtcNow(),
            request.Deadline, 1);
        await _services.GetRequiredService<ISecurityGrantStore>().RegisterAsync(grant, cancellationToken).ConfigureAwait(false);
        return new SecurityAllowed(request.Id, authorization.PolicySnapshot.Version, grant);
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }

    /// <inheritdoc/>
    public void Dispose() => _services.Dispose();

    private SessionBackedGoalStore NewStore() => new(
        _coordinator,
        _profile,
        Grants,
        new DelegateIds<SecurityEnforcementIntentId>(() => new SecurityEnforcementIntentId(NextGuid())),
        new DelegateIds<SessionEntryId>(() => new SessionEntryId(NextGuid())),
        _clock,
        new SessionBackedGoalStoreOptions());

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _next);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 7;
        return new Guid(bytes);
    }

    private sealed class DelegateIds<T>(Func<T> create): IIdentifierGenerator<T>
        where T : struct
    {
        public T Create() => create();
    }
}
