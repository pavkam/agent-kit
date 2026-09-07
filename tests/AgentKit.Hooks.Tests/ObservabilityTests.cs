// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

public sealed class ObservabilityTests
{
    [Fact]
    public async Task DispatchAsync_WhenObserved_EmitsCorrelatedTerminalActivity()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var args = new TestHookEventArgs();
        var dispatcher = new DefaultHookDispatcher();

        await dispatcher.DispatchAsync<TestHook, TestHookEventArgs>(
            new HookPointId("test.observed"),
            [],
            args,
            static (_, _, _, _) => Task.CompletedTask,
            HookDispatchScope.Root,
            cancellationToken: TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.HookDispatch);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.HookInvocationId).ShouldBe(args.InvocationId.ToString());
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
