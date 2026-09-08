// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Verifies portable deterministic, bounded, and cancellation behavior for promotion policies.</summary>
/// <typeparam name="TFixture">The independently composed fixture.</typeparam>
public abstract class InputPromotionPolicyConformanceTests<TFixture>
    where TFixture : IInputPromotionPolicyConformanceFixture
{
    /// <summary>Creates a fresh fixture owned by one test.</summary><returns>The disposable fixture.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies steering selection is complete and ordered by durable admission sequence.</summary>
    [Fact]
    public async Task PlanAsync_WhenSteeringBoundary_ReturnsEverySteerInAdmissionOrder()
    {
        using var fixture = CreateFixture();
        var result = await fixture.Policy.PlanAsync(fixture.CreateSteeringContext(), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<InputPromotionPlan>().Snapshot.AdmissionIds.ShouldBe(fixture.ExpectedSteeringAdmissions());
    }

    /// <summary>Verifies insufficient selection capacity rejects the whole plan.</summary>
    [Fact]
    public async Task PlanAsync_WhenRequiredSelectionExceedsBound_ReturnsTypedRejection()
    {
        using var fixture = CreateFixture();
        var result = await fixture.Policy.PlanAsync(fixture.CreateOverLimitContext(), TestContext.Current.CancellationToken);
        result.ShouldBeOfType<InputPromotionPlanRejected>().Kind.ShouldBe(InputPromotionPlanRejectionKind.SelectionLimitExceeded);
    }

    /// <summary>Verifies pre-cancellation propagates without returning partial selection evidence.</summary>
    [Fact]
    public async Task PlanAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var action = () => fixture.Policy.PlanAsync(fixture.CreateSteeringContext(), cancellation.Token).AsTask();
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
    }
}
