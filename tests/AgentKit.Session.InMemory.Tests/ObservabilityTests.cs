// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies the content-free concrete session-store diagnostics contract.</summary>
public sealed class ObservabilityTests
{
    [Fact]
    public async Task CreateAsync_WhenObserved_EmitsCorrelatedStoreActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var store = TestFactory.CreateStore();
        var request = TestFactory.CreateRequest();

        _ = await store.CreateAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.SessionStoreOperation);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(request.AgentId.ToString());
        activity.GetTagItem(AgentKitTagNames.SessionOperation).ShouldBe("create");
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
