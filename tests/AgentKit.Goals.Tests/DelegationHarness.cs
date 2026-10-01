// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

using AgentKit.Goals.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

/// <summary>Composes the real goal runtime over the in-memory store, a granting authority, and a fake clock.</summary>
internal sealed class DelegationHarness: IAsyncDisposable
{
    internal static GoalStoreKey StoreKey { get; } = new("memory");
    internal static DelegationDispatcherKey DispatcherKey { get; } = new("local");

    private readonly ServiceProvider _provider;

    internal DelegationHarness(Action<IServiceCollection>? configure = null, Action<GoalProfileOptions>? profile = null, IDelegationTargetProvider? targets = null, IEnumerable<AgentId>? targetAgents = null, Action<InMemoryGoalStoreOptions>? store = null)
    {
        Time = new FakeTimeProvider(GoalTestData.Now);
        Grants = new TestGoalGrants();
        Authority = new GrantingAuthority(Grants);
        Targets = targets ?? new StaticTargets([.. targetAgents ?? []]);
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(Time);
        _ = services.AddSingleton<ISecurityGrantStore>(Grants);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(Authority));
        _ = services.AddAgentGoals(static options => options.JoinPollInterval = TimeSpan.FromMilliseconds(100));
        _ = services.AddInMemoryGoalStore(StoreKey, store);
        _ = services.AddLocalDelegationDispatcher(DispatcherKey);
        _ = services.AddGoalProfile(GoalTestData.Profile.Key, options =>
        {
            options.StoreKey = StoreKey;
            options.DispatcherKey = DispatcherKey;
            options.Version = GoalTestData.Profile.Version;
            profile?.Invoke(options);
        });
        configure?.Invoke(services);
        _ = services.RemoveAll<IDelegationTargetProvider>();
        _ = services.AddSingleton(Targets);
        _provider = services.BuildServiceProvider(validateScopes: true);
    }

    internal FakeTimeProvider Time { get; }

    internal TestGoalGrants Grants { get; }

    internal GrantingAuthority Authority { get; }

    internal IDelegationTargetProvider Targets { get; }

    internal IDelegationCoordinator Delegation => _provider.GetRequiredService<IDelegationCoordinator>();

    internal IGoalCoordinator Goals => _provider.GetRequiredService<IGoalCoordinator>();

    internal T Get<T>()
        where T : notnull => _provider.GetRequiredService<T>();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    /// <summary>Creates a run context: a fresh agent, session, run, captured authorization, and the run's root goal identity.</summary>
    internal static RunContext NewRun(AgentId? agent = null)
    {
        var agentId = agent ?? GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        return new RunContext(agentId, session, run, GoalTestData.Authorization(agentId, session, run));
    }

    /// <summary>Creates a delegation from a run's implicit root goal.</summary>
    internal static DelegationRequest RequestFor(RunContext run, AgentId target, string key, GoalBudget? budget = null, TimeSpan? deadline = null)
    {
        var root = RunRootGoal.GoalIdFor(run.RunId);
        var parent = GoalTestData.Goal(run.AgentId, run.SessionId, run.RunId, id: root);
        var request = GoalTestData.Delegation(parent, RunRootGoal.AttemptIdFor(run.RunId), run.RunId, target, key, run.Authorization, budget);
        return deadline is { } span ? Rewrite(request, GoalTestData.Now + span) : request;
    }

    private static DelegationRequest Rewrite(DelegationRequest request, DateTimeOffset deadline) => new(
        request.Id, request.ParentGoalId, request.ParentAttemptId, request.ParentAgentId, request.ParentSessionId, request.ParentRunId,
        request.ProfileKey, request.ProfileVersion, request.AgentDefinitionRevision, request.Authorization, request.OperationId,
        request.TargetAgentId, request.ChildGoal, request.AcceptanceCriteria, request.Scope, request.Budget, deadline,
        request.CancellationMode, request.JoinStrategyKey, request.IdempotencyKey);

    /// <summary>Reads the parent's children in creation order.</summary>
    internal async Task<ImmutableArray<GoalRecord>> ChildrenAsync(RunContext run)
    {
        var page = await Goals.ReadChildrenAsync(
            new GoalChildrenCommand(GoalTestData.Profile, RunRootGoal.GoalIdFor(run.RunId), 0, 100, run.Authorization));
        return page is GoalPage read ? read.Items : [];
    }

    /// <summary>Advances the fake clock in poll-sized steps until the parent has a child with the wanted ordinal.</summary>
    internal async Task<GoalRecord> WaitForChildAsync(RunContext run, int count = 1)
    {
        for (var i = 0; i < 500; i++)
        {
            var children = await ChildrenAsync(run);
            if (children.Length >= count)
            {
                return children[count - 1];
            }

            Time.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Yield();
        }

        throw new TimeoutException("The child was never created.");
    }

    /// <summary>Plays the worker: claims a ready child, then completes it with a valid result.</summary>
    internal async Task<GoalRecord> CompleteChildAsync(GoalRecord child, string summary = "Done.", GoalStatus end = GoalStatus.Completed)
    {
        var authorization = child.Delegation!.Authorization;
        var childRun = GoalTestData.NewRun();
        var childSession = GoalTestData.NewSession();
        var started = (GoalAttemptStarted) await Goals.StartAttemptAsync(new GoalAttemptRequest(
            GoalTestData.Profile, child.Goal.Id, child.Goal.Version, child.Attempts.Length + 1, attemptId: null, child.Delegation.TargetAgentId, childSession, childRun,
            childRun, GoalTestData.NewOperation(), TransitionActor.Worker, child.Delegation.Budget, new IdempotencyKey($"claim:{child.Goal.Id}:{child.Attempts.Length}"), authorization));
        var active = started.Record;
        var now = Time.GetUtcNow();
        var outcome = end == GoalStatus.Completed ? GoalTestData.Outcome(childRun, summary) : null;
        var status = end switch
        {
            GoalStatus.Completed => GoalAttemptStatus.Succeeded,
            GoalStatus.Failed => GoalAttemptStatus.Failed,
            GoalStatus.Blocked => GoalAttemptStatus.Blocked,
            GoalStatus.Proposed or GoalStatus.Ready or GoalStatus.Active or GoalStatus.Waiting or GoalStatus.Cancelled => GoalAttemptStatus.Cancelled,
            _ => GoalAttemptStatus.Cancelled,
        };
        var transition = new GoalTransition(
            active.Goal.Id, active.Goal.OwnerAgentId, active.Goal.SessionId, childRun, GoalTestData.NewOperation(), GoalStatus.Active, end,
            TransitionActor.Worker, end == GoalStatus.Completed ? GoalTransitionReason.Completed : GoalTransitionReason.AttemptFailed, active.Goal.Version,
            new IdempotencyKey($"settle:{active.Goal.Id}:{active.Attempts.Length}"), now);
        var settled = await Goals.TransitionAsync(new GoalTransitionCommand(
            GoalTestData.Profile, transition, new GoalAttemptSettlement(active.ActiveAttempt!.Id, status, outcome, now), authorization));
        return ((GoalTransitioned) settled).Record;
    }

    /// <summary>Is one run's identities.</summary>
    internal sealed record RunContext(AgentId AgentId, SessionId SessionId, RunId RunId, SecurityAuthorizationContext Authorization);

    /// <summary>Is an authority that allows by issuing exactly bound grants, or denies on demand.</summary>
    internal sealed class GrantingAuthority(TestGoalGrants grants): ISecurityAuthority
    {
        internal bool Deny { get; set; }

        internal bool TamperDelegationGrants { get; set; }

        internal List<SecurityRequest> Requests { get; } = [];

        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            lock (Requests)
            {
                Requests.Add(request);
            }

            var authorization = request.Authorization;
            var version = authorization.PolicySnapshot.Version;
            if (Deny)
            {
                return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(request.Id, version, new SecurityDenial("denied", "Denied by test policy.")));
            }

            var fingerprint = TamperDelegationGrants && request.Kind == SecurityOperationKind.Delegation ? new InputFingerprint("tampered") : request.InputFingerprint;
            var grant = grants.Issue(request.Audience, authorization, request.Kind, request.Effect, request.Resources, fingerprint);
            return ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(request.Id, version, grant));
        }
    }

    /// <summary>Publishes a fixed set of target agents.</summary>
    internal sealed class StaticTargets(ImmutableArray<AgentId> agents): IDelegationTargetProvider
    {
        private static ComponentId Source { get; } = new("tests.targets");

        public ValueTask<DelegationTargetSnapshot> DiscoverAsync(DelegationDiscoveryRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new DelegationTargetSnapshot(
                Source, [.. agents.Select(static agent => new DelegationTarget(agent, new AgentDefinitionRevision(1), Source, []))]));
    }
}
