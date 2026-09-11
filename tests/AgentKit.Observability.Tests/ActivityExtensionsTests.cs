// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;



/// <summary>Verifies ActivityExtensions behavior and contracts.</summary>
public sealed class ActivityExtensionsTests
{
    [Fact]
    public void ActivityExtensions_WhenOperationSucceeds_RecordStableTerminalState()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        using (var activity = AgentKitDiagnostics.Activities.StartActivity("test.operation"))
        {
            activity.SetSuccessful("completed");
        }

        var completed = stopped.ShouldNotBeNull();
        completed.Status.ShouldBe(ActivityStatusCode.Ok);
        completed.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("completed");
    }

    [Fact]
    public void ActivityExtensions_WhenNoListenerExists_DoNotRequireAnActivity()
    {
        Activity? activity = null;
        Should.NotThrow(() => activity.SetFailed("failed", "test_error"));
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
}
