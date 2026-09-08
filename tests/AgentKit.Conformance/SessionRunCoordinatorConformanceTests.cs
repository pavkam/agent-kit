// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable exact per-lane ownership behavior for replaceable run coordinators.</summary>
/// <typeparam name="TFixture">The implementation fixture that supplies a real accepted operation.</typeparam>
public abstract class SessionRunCoordinatorConformanceTests<TFixture>
    where TFixture : ISessionRunCoordinatorConformanceFixture
{
    /// <summary>Creates an isolated implementation fixture.</summary><returns>A fresh disposable fixture.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies acquisition returns the exact owner that protected recovery established.</summary>
    [Fact]
    public async Task AcquireAsync_WhenProtectedAcceptedStateMatches_ReturnsExactLease()
    {
        await using var fixture = CreateFixture();
        var scenario = await fixture.CreateAcceptedScenarioAsync(TestContext.Current.CancellationToken);

        var result = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Session,
            TestContext.Current.CancellationToken);

        var lease = result.ShouldBeOfType<SessionRunLeaseAcquired>().Lease;
        await using var ownedLease = lease;
        lease.OperationId.ShouldBe(scenario.AcceptedState.Correlation.OperationId);
        lease.RunId.ShouldBe(scenario.AcceptedState.Correlation.RunId);
        lease.ExecutionLaneId.ShouldBe(scenario.AcceptedState.ExecutionLaneId);
        lease.StateRevision.ShouldBe(scenario.AcceptedState.OperationStateRevision);
    }

    /// <summary>Verifies the same lane reports its actual owner while a different operation cannot enter.</summary>
    [Fact]
    public async Task AcquireAsync_WhenLaneIsOwned_ReturnsActualOperationAndRun()
    {
        await using var fixture = CreateFixture();
        var scenario = await fixture.CreateAcceptedScenarioAsync(TestContext.Current.CancellationToken);
        var acquired = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Session, TestContext.Current.CancellationToken);
        await using var ownedLease = acquired.Lease;

        var busy = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Session,
            TestContext.Current.CancellationToken);

        var owner = busy.ShouldBeOfType<SessionRunBusy>();
        owner.ActiveOperationId.ShouldBe(scenario.Request.OperationId);
        owner.ActiveRunId.ShouldBe(scenario.Request.RunId);
    }

    /// <summary>Verifies disposal releases exact ownership and permits recovery-driven reacquisition.</summary>
    [Fact]
    public async Task DisposeAsync_WhenLeaseIsCurrent_AllowsExactReacquisition()
    {
        await using var fixture = CreateFixture();
        var scenario = await fixture.CreateAcceptedScenarioAsync(TestContext.Current.CancellationToken);
        var first = (SessionRunLeaseAcquired) await scenario.Coordinator.AcquireAsync(
            scenario.Request, scenario.Session, TestContext.Current.CancellationToken);
        await first.Lease.DisposeAsync();

        var second = await scenario.Coordinator.AcquireAsync(scenario.Request, scenario.Session,
            TestContext.Current.CancellationToken);

        await using var secondLease = second.ShouldBeOfType<SessionRunLeaseAcquired>().Lease;
    }

    /// <summary>Verifies different lanes in one session never collapse into a session-wide lock.</summary>
    [Fact]
    public async Task AcquireAsync_WhenDifferentLanesShareSession_BothAcquireIndependently()
    {
        await using var fixture = CreateFixture();
        var pair = await fixture.CreateDifferentLaneScenariosAsync(TestContext.Current.CancellationToken);
        var first = (SessionRunLeaseAcquired) await pair.First.Coordinator.AcquireAsync(
            pair.First.Request, pair.First.Session, TestContext.Current.CancellationToken);
        await using var firstLease = first.Lease;

        var second = await pair.Second.Coordinator.AcquireAsync(
            pair.Second.Request, pair.Second.Session, TestContext.Current.CancellationToken);

        await using var secondLease = second.ShouldBeOfType<SessionRunLeaseAcquired>().Lease;
    }

    /// <summary>Verifies colliding coordinates in different tenants neither block nor disclose the other owner.</summary>
    [Fact]
    public async Task AcquireAsync_WhenDifferentTenantsShareCoordinates_BothAcquireIndependently()
    {
        await using var fixture = CreateFixture();
        var pair = await fixture.CreateDifferentTenantScenariosAsync(TestContext.Current.CancellationToken);
        var first = (SessionRunLeaseAcquired) await pair.First.Coordinator.AcquireAsync(
            pair.First.Request, pair.First.Session, TestContext.Current.CancellationToken);
        await using var firstLease = first.Lease;

        var second = await pair.Second.Coordinator.AcquireAsync(
            pair.Second.Request, pair.Second.Session, TestContext.Current.CancellationToken);

        var acquired = second.ShouldBeOfType<SessionRunLeaseAcquired>();
        await using var secondLease = acquired.Lease;
        acquired.Lease.TenantId.ShouldBe(pair.Second.Request.Context.Identity.TenantId);
    }
}
