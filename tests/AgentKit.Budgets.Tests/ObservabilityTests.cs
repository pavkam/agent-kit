// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class ObservabilityTests
{
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

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
