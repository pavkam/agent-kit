// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

using System.Collections.Concurrent;

public sealed class ObservabilityTests
{
    private static readonly BudgetDimension _observedDimension = new("test.observability.dimension");

    [Fact]
    public async Task ReserveAsync_WhenObserved_EmitsCorrelatedTerminalActivity()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var request = TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 1m);
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.BudgetReserve
                    && Equals(activity.GetTagItem(AgentKitTagNames.OperationId), request.OperationId.ToString()))
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        _ = await scope.ReserveAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.BudgetScopeId).ShouldBe(scope.Id.ToString());
        activity.GetTagItem(AgentKitTagNames.BudgetDimension).ShouldBe(TestFactory.TestDimension.ToString());
    }

    [Fact]
    public async Task StartAndCorrection_WhenObserved_EmitTruthfulBoundedTerminalActivities()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var outcomes = new List<(string Name, string Outcome)>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if ((activity.OperationName is AgentKitActivityNames.BudgetStart
                    or AgentKitActivityNames.BudgetCommit
                    or AgentKitActivityNames.BudgetCorrection)
                    && Equals(activity.GetTagItem(AgentKitTagNames.BudgetScopeId), scope.Id.ToString()))
                {
                    outcomes.Add((activity.OperationName, (string) activity.GetTagItem(AgentKitTagNames.Outcome)!));
                    activity.GetTagItem(AgentKitTagNames.BudgetDimension).ShouldBe(TestFactory.TestDimension.ToString());
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var reservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension, 2m),
            TestContext.Current.CancellationToken)).Reservation;

        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reservation.CommitAsync(2m, TestContext.Current.CancellationToken);
        _ = await reservation.CorrectAsync(1m, 1, TestContext.Current.CancellationToken);

        outcomes.ShouldBe(
        [
            (AgentKitActivityNames.BudgetStart, "started"),
            (AgentKitActivityNames.BudgetCommit, "committed"),
            (AgentKitActivityNames.BudgetCorrection, "corrected"),
        ]);
    }

    [Fact]
    public async Task StartAndCorrection_WhenCancelled_EmitTruthfulCancelledActivities()
    {
        var authority = TestFactory.Authority();
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var activities = new List<(string Name, string Outcome)>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.OperationName is AgentKitActivityNames.BudgetStart or AgentKitActivityNames.BudgetCorrection)
                {
                    activities.Add((activity.OperationName, (string) activity.GetTagItem(AgentKitTagNames.Outcome)!));
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var startReservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension),
            TestContext.Current.CancellationToken)).Reservation;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => startReservation.MarkStartedAsync(cancelled.Token).AsTask());

        var correctionReservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, TestFactory.TestDimension),
            TestContext.Current.CancellationToken)).Reservation;
        _ = await correctionReservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await correctionReservation.CommitAsync(1m, TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => correctionReservation.CorrectAsync(1m, 1, cancelled.Token).AsTask());

        activities.ShouldContain((AgentKitActivityNames.BudgetStart, "cancelled"));
        activities.ShouldContain((AgentKitActivityNames.BudgetCorrection, "cancelled"));
    }

    [Fact]
    public async Task StartAndCorrection_WhenMeasured_UseDistinctCountersWithOnlyBoundedTags()
    {
        var measurements = new ConcurrentQueue<(string Name, string Outcome, string Dimension, bool HasScope)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name is AgentKitMetricNames.BudgetStartCount
                        or AgentKitMetricNames.BudgetCorrectionCount)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            string? outcome = null;
            string? dimension = null;
            var hasScope = false;
            foreach (var tag in tags)
            {
                if (tag.Key == AgentKitTagNames.Outcome)
                {
                    outcome = (string?) tag.Value;
                }
                else if (tag.Key == AgentKitTagNames.BudgetDimension)
                {
                    dimension = (string?) tag.Value;
                }
                else if (tag.Key == AgentKitTagNames.BudgetScopeId)
                {
                    hasScope = true;
                }
            }

            if (dimension == _observedDimension.Value)
            {
                measurements.Enqueue((instrument.Name, outcome!, dimension, hasScope));
            }
        });
        listener.Start();
        var authority = TestFactory.Authority(dimensions: TestFactory.DefaultCatalog(
            new BudgetDimensionDescriptor(_observedDimension, BudgetAggregationKind.Sum, [TestFactory.Count])));
        var scope = await TestFactory.CreateRootScopeAsync(authority);
        var reservation = ((BudgetReserved) await scope.ReserveAsync(
            TestFactory.ReservationRequest(scope.Id, _observedDimension),
            TestContext.Current.CancellationToken)).Reservation;

        _ = await reservation.MarkStartedAsync(TestContext.Current.CancellationToken);
        _ = await reservation.CommitAsync(1m, TestContext.Current.CancellationToken);
        _ = await reservation.CorrectAsync(1m, 1, TestContext.Current.CancellationToken);

        measurements.ShouldContain((AgentKitMetricNames.BudgetStartCount, "started", _observedDimension.Value, false));
        measurements.ShouldContain((AgentKitMetricNames.BudgetCorrectionCount, "corrected", _observedDimension.Value, false));
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
