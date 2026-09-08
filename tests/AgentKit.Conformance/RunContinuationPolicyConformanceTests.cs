// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable behavioral cases for deterministic continuation policies.</summary>
/// <typeparam name="TFixture">The implementation fixture.</typeparam>
public abstract class RunContinuationPolicyConformanceTests<TFixture>
    where TFixture : IRunContinuationPolicyConformanceFixture
{
    /// <summary>Creates an isolated fixture.</summary>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies committed required stops outrank continuation causes.</summary>
    [Fact]
    public async Task DecideAsync_WhenRequiredStopExists_HaltsWithExactOutcome()
    {
        var fixture = CreateFixture();
        var stop = new AgentRunTurnLimitReached(3);
        var context = fixture.CreateContext(
            new IdleContinuationBoundary(),
            [new ExplicitPolicyContinuationCause("follow-up")],
            stop);

        var decision = await fixture.Policy.DecideAsync(context, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeSameAs(stop);
    }

    /// <summary>Verifies an idle boundary with no work proposes idle completion.</summary>
    [Fact]
    public async Task DecideAsync_WhenIdleAndNoCause_CompletesIdle()
    {
        var fixture = CreateFixture();
        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), []),
            TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<CompleteRun>().Outcome.ShouldBeOfType<AgentRunIdle>();
    }

    /// <summary>Verifies optional policy continuation retains every lower-priority pending cause.</summary>
    [Fact]
    public async Task DecideAsync_WhenSeveralCausesExist_RetainsUnselectedCauses()
    {
        var fixture = CreateFixture();
        var first = new ExplicitPolicyContinuationCause("first");
        var second = new ExplicitPolicyContinuationCause("second");

        var decision = await fixture.Policy.DecideAsync(
            fixture.CreateContext(new IdleContinuationBoundary(), [first, second]),
            TestContext.Current.CancellationToken);

        var reason = decision.ShouldBeOfType<ContinueRun>().Reason;
        reason.SelectedCause.ShouldBeSameAs(first);
        reason.OtherPendingCauses.ShouldBe([second]);
    }

    /// <summary>Verifies terminal processor rejection cannot be bypassed by another pending cause.</summary>
    [Fact]
    public async Task DecideAsync_WhenOutputIsRejectedAndCauseExists_HaltsOutput()
    {
        var fixture = CreateFixture();
        var rejected = new OutputRejected(new OutputValidationFailure(
            OutputValidationFailureKind.ValidatorFailed, "Rejected.", []));
        var context = fixture.CreateContext(
            fixture.CreateCommittedBoundary(rejected, requiresOutput: true),
            [new ExplicitPolicyContinuationCause("follow-up")]);

        var decision = await fixture.Policy.DecideAsync(context, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<HaltRun>().Outcome.ShouldBeOfType<AgentRunOutputRejected>().Rejection.ShouldBeSameAs(rejected);
    }

    /// <summary>Verifies pre-cancellation discards evaluation without producing a proposal.</summary>
    [Fact]
    public async Task DecideAsync_WhenPreCancelled_ThrowsOperationCanceledException()
    {
        var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await fixture.Policy.DecideAsync(
                fixture.CreateContext(new IdleContinuationBoundary(), []), cancellation.Token));
    }
}
