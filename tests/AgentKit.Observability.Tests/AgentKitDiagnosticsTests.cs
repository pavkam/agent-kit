// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

public sealed class AgentKitDiagnosticsTests
{
    [Fact]
    public void Diagnostics_WhenAccessed_ExposeStableMicrosoftSources()
    {
        AgentKitDiagnostics.Activities.Name.ShouldBe(AgentKitDiagnostics.ActivitySourceName);
        AgentKitDiagnostics.Metrics.Name.ShouldBe(AgentKitDiagnostics.MeterName);
        AgentKitDiagnostics.ActivitySourceName.ShouldBe("AgentKit");
        AgentKitDiagnostics.MeterName.ShouldBe("AgentKit");
    }

    [Fact]
    public void AddAgentKitObservability_WhenCalledTwice_PreservesOneLoggingFoundation()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentKitObservability().AddAgentKitObservability();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ILoggerFactory>().ShouldNotBeNull();
        _ = provider.GetRequiredService<ILogger<AgentKitDiagnosticsTests>>().ShouldNotBeNull();
    }

    [Fact]
    public void StableNames_WhenInspected_ContainNoHighCardinalityValues()
    {
        AgentKitActivityNames.InvokeAgent.ShouldBe("invoke_agent");
        AgentKitActivityNames.Chat.ShouldBe("chat");
        AgentKitActivityNames.ExecuteTool.ShouldBe("execute_tool");
        AgentKitTagNames.GenAiOperationName.ShouldBe("gen_ai.operation.name");
        AgentKitTagNames.ErrorType.ShouldBe("error.type");
    }

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

    [Fact]
    public void AddAgentKitObservability_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(services.AddAgentKitObservability);

        exception.ParamName.ShouldBe("services");
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}
