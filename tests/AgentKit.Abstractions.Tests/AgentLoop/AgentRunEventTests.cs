// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunEvent behavior and contracts.</summary>
public sealed class AgentRunEventTests
{
    [Fact]
    public void AgentRunEvent_Hierarchy_EveryLeafDerivesFromAgentRunEvent()
    {
        AgentRunEvent modelResponse = new AgentRunModelResponseEvent(LoopTestData.TurnId, new ModelResponseStarted(LoopTestData.ModelRequestId, 0));
        AgentRunEvent toolStarted = new AgentRunToolCallStarted(LoopTestData.TurnId, LoopTestData.ToolCall());
        AgentRunEvent toolCompleted = new AgentRunToolCallCompleted(LoopTestData.TurnId, LoopTestData.ToolResult());
        _ = modelResponse.ShouldBeOfType<AgentRunModelResponseEvent>();
        _ = toolStarted.ShouldBeOfType<AgentRunToolCallStarted>();
        _ = toolCompleted.ShouldBeOfType<AgentRunToolCallCompleted>();
    }
}
