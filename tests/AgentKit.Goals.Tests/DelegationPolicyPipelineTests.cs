// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Tests;

public sealed class DelegationPolicyPipelineTests
{
    private sealed class Recorder
    {
        internal List<string> Calls { get; } = [];
    }

    private sealed class FirstPolicy(Recorder recorder): IDelegationPolicy
    {
        public ValueTask<DelegationPolicyDecision> EvaluateAsync(DelegationRequest request, DelegationPolicyContext context, CancellationToken cancellationToken = default)
        {
            recorder.Calls.Add("first");
            return ValueTask.FromResult<DelegationPolicyDecision>(new DelegationPolicyAllowed());
        }
    }

    private sealed class SecondPolicy(Recorder recorder): IDelegationPolicy
    {
        public ValueTask<DelegationPolicyDecision> EvaluateAsync(DelegationRequest request, DelegationPolicyContext context, CancellationToken cancellationToken = default)
        {
            recorder.Calls.Add("second");
            return ValueTask.FromResult<DelegationPolicyDecision>(new DelegationPolicyAllowed());
        }
    }

    private sealed class NarrowingPolicy: IDelegationPolicy
    {
        public ValueTask<DelegationPolicyDecision> EvaluateAsync(DelegationRequest request, DelegationPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<DelegationPolicyDecision>(new DelegationPolicyAllowed(budget: new GoalBudget(1, 1, 0)));
    }

    private sealed class WideningPolicy: IDelegationPolicy
    {
        public ValueTask<DelegationPolicyDecision> EvaluateAsync(DelegationRequest request, DelegationPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<DelegationPolicyDecision>(new DelegationPolicyAllowed(budget: new GoalBudget(99, 99, 0)));
    }

    private sealed class ScopeWideningPolicy: IDelegationPolicy
    {
        public ValueTask<DelegationPolicyDecision> EvaluateAsync(DelegationRequest request, DelegationPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<DelegationPolicyDecision>(new DelegationPolicyAllowed(scope: new DelegationScope([new ToolId("extra")], [])));
    }

    private sealed class DenyingPolicy: IDelegationPolicy
    {
        public ValueTask<DelegationPolicyDecision> EvaluateAsync(DelegationRequest request, DelegationPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<DelegationPolicyDecision>(new DelegationPolicyDenied(new DelegationRejection(DelegationRejectionKind.PolicyDenied, "No.")));
    }

    private static readonly ComponentId _firstId = new("first");
    private static readonly ComponentId _secondId = new("second");

    private static async Task<DelegationPolicyDecision> Evaluate(Action<IServiceCollection> register, params ComponentId[] selected)
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(configure: register, profile: options => options.PolicyIds.AddRange(selected), targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var request = DelegationHarness.RequestFor(run, target, "p");
        var parent = new GoalRecord(
            GoalTestData.Goal(run.AgentId, run.SessionId, run.RunId, id: RunRootGoal.GoalIdFor(run.RunId)),
            [], [], null, 1, null, null);
        var catalog = harness.Get<IGoalProfileCatalog>();
        _ = catalog.TryGet(GoalTestData.Profile, out var profile);
        var context = new DelegationPolicyContext(profile!, parent, 0, 0, new DelegationTarget(target, new AgentDefinitionRevision(1), new ComponentId("s"), []), GoalTestData.Now);
        return await harness.Get<IDelegationPolicyPipeline>().EvaluateAsync(request, context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPoliciesDeclareOrdering_RunsThemInDeclaredOrderAfterBaseline()
    {
        var recorder = new Recorder();

        var decision = await Evaluate(
            services =>
            {
                _ = services.AddSingleton(recorder);
                _ = services.AddDelegationPolicy<FirstPolicy>(new DelegationPolicyRegistration(_firstId, [_secondId]));
                _ = services.AddDelegationPolicy<SecondPolicy>(new DelegationPolicyRegistration(_secondId));
            },
            _firstId,
            _secondId);

        _ = decision.ShouldBeOfType<DelegationPolicyAllowed>();
        recorder.Calls.ShouldBe(["second", "first"]);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPolicyNarrowsBudget_ReturnsNarrowedBudget()
    {
        var decision = await Evaluate(static services => services.AddDelegationPolicy<NarrowingPolicy>(new DelegationPolicyRegistration(_firstId)), _firstId);

        decision.ShouldBeOfType<DelegationPolicyAllowed>().Budget.ShouldBe(new GoalBudget(1, 1, 0));
    }

    [Fact]
    public async Task EvaluateAsync_WhenPolicyWidensBudget_DeniesAsPolicyViolation()
    {
        var decision = await Evaluate(static services => services.AddDelegationPolicy<WideningPolicy>(new DelegationPolicyRegistration(_firstId)), _firstId);

        decision.ShouldBeOfType<DelegationPolicyDenied>().Rejection.Kind.ShouldBe(DelegationRejectionKind.PolicyDenied);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPolicyWidensToolScope_DeniesAsPolicyViolation()
    {
        var decision = await Evaluate(static services => services.AddDelegationPolicy<ScopeWideningPolicy>(new DelegationPolicyRegistration(_firstId)), _firstId);

        decision.ShouldBeOfType<DelegationPolicyDenied>().Rejection.Kind.ShouldBe(DelegationRejectionKind.PolicyDenied);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPolicyDenies_StopsWithThatDenialBeforeLaterPolicies()
    {
        var recorder = new Recorder();

        var decision = await Evaluate(
            services =>
            {
                _ = services.AddSingleton(recorder);
                _ = services.AddDelegationPolicy<DenyingPolicy>(new DelegationPolicyRegistration(_firstId));
                _ = services.AddDelegationPolicy<SecondPolicy>(new DelegationPolicyRegistration(_secondId, [_firstId]));
            },
            _firstId,
            _secondId);

        _ = decision.ShouldBeOfType<DelegationPolicyDenied>();
        recorder.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_WhenSelectedPolicyIsNotRegistered_FailsClosed()
    {
        var decision = await Evaluate(static _ => { }, new ComponentId("missing"));

        decision.ShouldBeOfType<DelegationPolicyDenied>().Rejection.Kind.ShouldBe(DelegationRejectionKind.PolicyDenied);
    }

    [Fact]
    public async Task EvaluateAsync_WhenOrderingIsCyclic_FailsClosed()
    {
        var decision = await Evaluate(
            static services =>
            {
                _ = services.AddSingleton<Recorder>();
                _ = services.AddDelegationPolicy<FirstPolicy>(new DelegationPolicyRegistration(_firstId, [_secondId]));
                _ = services.AddDelegationPolicy<SecondPolicy>(new DelegationPolicyRegistration(_secondId, [_firstId]));
            },
            _firstId,
            _secondId);

        decision.ShouldBeOfType<DelegationPolicyDenied>().Rejection.Kind.ShouldBe(DelegationRejectionKind.PolicyDenied);
    }

    [Fact]
    public async Task EvaluateAsync_WhenBaselineSeesWiderChildBudgetThanParent_DeniesUnauthorized()
    {
        var target = GoalTestData.NewAgent();
        await using var harness = new DelegationHarness(targetAgents: [target]);
        var run = DelegationHarness.NewRun();
        var request = DelegationHarness.RequestFor(run, target, "p", new GoalBudget(500, 1, 0));
        var parent = new GoalRecord(GoalTestData.Goal(run.AgentId, run.SessionId, run.RunId), [], [], null, 1, null, null);
        _ = harness.Get<IGoalProfileCatalog>().TryGet(GoalTestData.Profile, out var profile);
        var context = new DelegationPolicyContext(profile!, parent, 0, 0, new DelegationTarget(target, new AgentDefinitionRevision(1), new ComponentId("s"), []), GoalTestData.Now);

        var decision = await harness.Get<IDelegationPolicyPipeline>().EvaluateAsync(request, context, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<DelegationPolicyDenied>().Rejection.Kind.ShouldBe(DelegationRejectionKind.Unauthorized);
    }
}
