// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies policy combination: explicit allow, any deny wins, and every failure refuses.</summary>
public sealed class DefaultMemoryPolicyDispatcherTests
{
    private sealed class ThrowingPolicy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The policy failed.");
    }

    private sealed class DenyPolicy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<MemoryPolicyDecision>(new MemoryPolicyDenied(new ComponentId("tests.deny"), "nope", "Refused."));
    }

    private sealed class CancellingPolicy: IMemoryPolicy
    {
        public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(cancellationToken);
    }

    private static MemoryPolicyRegistration Registration(string id, int order) => new(MemoryHarness.PolicyProfile, new ComponentId(id), order, ServiceLifetime.Singleton);

    private static async Task<MemoryPolicyDecision> EvaluateAsync(MemoryHarness harness, CancellationToken cancellationToken = default)
    {
        var owner = MemoryTestData.NewOwner();
        _ = harness.Provider.GetRequiredService<IMemoryProfileCatalog>().TryGet(MemoryTestData.ProfileKey, out var profile);
        return await harness.Provider.GetRequiredService<IMemoryPolicyDispatcher>()
            .EvaluateAsync(MemoryTestData.Proposal(owner), new MemoryPolicyContext(profile!, MemoryTestData.Now), cancellationToken);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAPolicyAllowsAndNoneDeny_Allows()
    {
        using var harness = MemoryHarness.Create();

        _ = (await EvaluateAsync(harness, TestContext.Current.CancellationToken)).ShouldBeOfType<MemoryPolicyAllowed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenAnyPolicyDenies_Denies()
    {
        using var harness = MemoryHarness.Create(arrange: services => services.AddMemoryPolicy<DenyPolicy>(Registration("tests.deny", 5)));

        var decision = await EvaluateAsync(harness, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<MemoryPolicyDenied>().Code.ShouldBe("nope");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAPolicyThrows_DeniesFailClosed()
    {
        using var harness = MemoryHarness.Create(arrange: services => services.AddMemoryPolicy<ThrowingPolicy>(Registration("tests.throwing", 5)));

        var decision = await EvaluateAsync(harness, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<MemoryPolicyDenied>().Code.ShouldBe("policy-failed");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAPolicyIsCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create(arrange: services => services.AddMemoryPolicy<CancellingPolicy>(Registration("tests.cancelling", 5)));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await EvaluateAsync(harness, source.Token));
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoPolicyIsRegisteredForTheProfile_DeniesByDefault()
    {
        using var harness = MemoryHarness.Create(allowPolicy: false);

        var decision = await EvaluateAsync(harness, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<MemoryPolicyDenied>().Code.ShouldBe("no-explicit-allow");
    }

    [Fact]
    public async Task EvaluateAsync_WhenAcceptanceModeAllowsUnlessDenied_AllowsWithNoPolicy()
    {
        using var harness = MemoryHarness.Create(allowPolicy: false, options: options => options.AcceptanceMode = MemoryAcceptanceMode.AllowUnlessPolicyDenies);

        _ = (await EvaluateAsync(harness, TestContext.Current.CancellationToken)).ShouldBeOfType<MemoryPolicyAllowed>();
    }

    [Fact]
    public async Task EvaluateAsync_WhenArgumentsAreNull_ThrowArgumentNullException()
    {
        using var harness = MemoryHarness.Create();
        var dispatcher = harness.Provider.GetRequiredService<IMemoryPolicyDispatcher>();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await dispatcher.EvaluateAsync(null!, null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("proposal");
        _ = harness.Provider.GetRequiredService<IMemoryProfileCatalog>().TryGet(MemoryTestData.ProfileKey, out _);
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await dispatcher.EvaluateAsync(MemoryTestData.Proposal(MemoryTestData.NewOwner()), null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("context");
    }
}
