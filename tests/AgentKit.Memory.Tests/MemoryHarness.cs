// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Composes the memory runtime over in-memory stores, a recording authority, a fake budget authority, and a controllable clock.</summary>
internal sealed class MemoryHarness: IDisposable
{
    internal static MemoryStoreKey StoreKey { get; } = new("memory-store");

    internal static MemoryPolicyProfileKey PolicyProfile { get; } = new("allow");

    private MemoryHarness(ServiceProvider provider, TestGoalGrants grants, RecordingAuthority authority, FakeTimeProvider time)
    {
        Provider = provider;
        Grants = grants;
        Authority = authority;
        Time = time;
    }

    internal ServiceProvider Provider { get; }

    internal TestGoalGrants Grants { get; }

    internal RecordingAuthority Authority { get; }

    internal FakeTimeProvider Time { get; }

    internal IMemoryCoordinator Coordinator => Provider.GetRequiredService<IMemoryCoordinator>();

    internal IRetrievalPipeline Pipeline => Provider.GetRequiredService<IRetrievalPipeline>();

    internal IMemoryStore Store => Provider.GetRequiredKeyedService<IMemoryStore>(StoreKey.Value);

    internal static MemoryHarness Create(
        Action<IServiceCollection>? arrange = null,
        Action<MemoryProfileOptions>? profile = null,
        bool allowPolicy = true,
        Action<AgentMemoryOptions>? options = null)
    {
        var time = new FakeTimeProvider(MemoryTestData.Now);
        var grants = new TestGoalGrants();
        var authority = new RecordingAuthority(grants);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(time);
        _ = services.AddSingleton<ISecurityGrantStore>(grants);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(authority));
        _ = services.AddSingleton<IBudgetAuthority, PermissiveBudgetAuthority>();
        _ = services.AddLogging();
        _ = services.AddAgentMemory(options);
        _ = services.AddInMemoryMemoryStore(StoreKey);
        _ = services.AddDurableMemoryRetrievalSource();
        if (allowPolicy)
        {
            _ = services.AddMemoryPolicy<AllowPolicy>(new MemoryPolicyRegistration(PolicyProfile, new ComponentId("tests.allow"), 0, ServiceLifetime.Singleton));
        }

        _ = services.AddMemoryProfile(MemoryTestData.ProfileKey, configured =>
        {
            configured.EnableDurableMemory = true;
            configured.EnableRetrieval = true;
            configured.MemoryStore = StoreKey;
            configured.RetrievalSources = [MemoryRetrievalSourceKeys.DurableMemory];
            configured.PolicyProfile = PolicyProfile;
            configured.MaximumClassification = DataClassification.Confidential;
            profile?.Invoke(configured);
        });
        arrange?.Invoke(services);
        return new MemoryHarness(services.BuildServiceProvider(), grants, authority, time);
    }

    public void Dispose() => Provider.Dispose();

    /// <summary>Is a policy that allows every proposal.</summary>
    internal sealed class AllowPolicy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<MemoryPolicyDecision>(new MemoryPolicyAllowed(new ComponentId("tests.allow")));
    }

    /// <summary>Is an authority that issues exactly bound grants, or denies on demand, recording every request.</summary>
    internal sealed class RecordingAuthority(TestGoalGrants grants): ISecurityAuthority
    {
        internal bool Deny { get; set; }

        internal bool Throw { get; set; }

        internal SecurityEffect? DenyEffect { get; set; }

        internal SecurityEffect? ThrowEffect { get; set; }

        internal List<SecurityRequest> Requests { get; } = [];

        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            lock (Requests)
            {
                Requests.Add(request);
            }

            if (Throw || ThrowEffect == request.Effect)
            {
                throw new InvalidOperationException("The authority failed.");
            }

            var authorization = request.Authorization;
            var version = authorization.PolicySnapshot.Version;
            if (Deny || DenyEffect == request.Effect)
            {
                return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(request.Id, version, new SecurityDenial("denied", "Denied by test policy.")));
            }

            var grant = grants.Issue(request.Audience, authorization, request.Kind, request.Effect, request.Resources, request.InputFingerprint);
            return ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(request.Id, version, grant));
        }
    }

    /// <summary>Is a budget authority that always creates a scope that refuses direct reservation.</summary>
    internal sealed class PermissiveBudgetAuthority: IBudgetAuthority
    {
        public ValueTask<BudgetScopeResult> CreateChildScopeAsync(BudgetScopeRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return ValueTask.FromResult<BudgetScopeResult>(new BudgetScopeCreated(new UnusedScope(request.Address)));
        }
    }

    private sealed class UnusedScope(BudgetScopeAddress address): IBudgetScope
    {
        public BudgetScopeId Id { get; } = new(Guid.NewGuid());

        public BudgetScopeAddress Address { get; } = address;

        public ValueTask<BudgetReservationResult> ReserveAsync(BudgetReservationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
