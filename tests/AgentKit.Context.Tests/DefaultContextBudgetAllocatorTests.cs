// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

/// <summary>Verifies <see cref="DefaultContextBudgetAllocator"/> selection policy.</summary>
public sealed class DefaultContextBudgetAllocatorTests
{
    [Fact]
    public async Task AllocateAsync_WhenMandatoryCandidatesExceedBudget_ReturnsMandatoryOverflow()
    {
        var estimator = new FixedEstimator(100);
        var allocator = new DefaultContextBudgetAllocator(estimator);
        var mandatory = ContextBudgetTestData.Candidate(100, mandatory: true);
        var request = new ContextBudgetRequest(
            new ContextBudget(200, reservedOutputTokens: 50, providerOverheadTokens: 0, estimationSafetyMargin: 0),
            [mandatory, mandatory],
            ContextOverflowBehavior.Fail);

        var plan = await allocator.AllocateAsync(request, TestContext.Current.CancellationToken);

        plan.MandatoryOverflow.ShouldBeTrue();
        plan.Selected.ShouldBeEmpty();
    }

    [Fact]
    public async Task AllocateAsync_WhenOptionalCandidatesDoNotFit_OmitsLowerPriorityFirst()
    {
        var estimator = new FixedEstimator(10);
        var allocator = new DefaultContextBudgetAllocator(estimator);
        var high = ContextBudgetTestData.Candidate(50, priority: 10);
        var low = ContextBudgetTestData.Candidate(50, priority: 1);
        var request = new ContextBudgetRequest(
            new ContextBudget(75, reservedOutputTokens: 0, providerOverheadTokens: 0, estimationSafetyMargin: 0),
            [low, high],
            ContextOverflowBehavior.Fail);

        var plan = await allocator.AllocateAsync(request, TestContext.Current.CancellationToken);

        plan.Selected.ShouldBe([high]);
        plan.Omitted.ShouldBe([low]);
    }

    private sealed class FixedEstimator(long tokens): IContextMessageTokenEstimator
    {
        public long EstimateTokens(ImmutableArray<AgentMessage> messages) => tokens;

        public long EstimateTokens(ImmutableArray<ContentPart> parts) => tokens;
    }
}
