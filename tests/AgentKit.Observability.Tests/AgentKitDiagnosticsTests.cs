// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;



/// <summary>Verifies AgentKitDiagnostics behavior and contracts.</summary>
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
    public void StableNames_WhenInspected_ContainNoHighCardinalityValues()
    {
        AgentKitActivityNames.AgentCompositionBuild.ShouldBe("agent.composition.build");
        AgentKitMetricNames.AgentCompositionBuildCount.ShouldBe("agentkit.agent.composition.build.count");
        AgentKitMetricNames.AgentCompositionBuildDuration.ShouldBe("agentkit.agent.composition.build.duration");
        AgentKitActivityNames.InvokeAgent.ShouldBe("invoke_agent");
        AgentKitActivityNames.Chat.ShouldBe("chat");
        AgentKitActivityNames.ExecuteTool.ShouldBe("execute_tool");
        AgentKitTagNames.GenAiOperationName.ShouldBe("gen_ai.operation.name");
        AgentKitTagNames.ErrorType.ShouldBe("error.type");
    }
}
