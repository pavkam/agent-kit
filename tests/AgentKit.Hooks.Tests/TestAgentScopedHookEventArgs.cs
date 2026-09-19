// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>
/// A synthetic agent-scoped hook event-argument type used only by this test
/// project to exercise <see cref="DefaultHookDispatcher"/>'s agent/session
/// activity tagging for a hook point whose stage has resolved an agent.
/// </summary>
internal sealed class TestAgentScopedHookEventArgs: AgentScopedHookEventArgs
{
    public TestAgentScopedHookEventArgs(AgentId agentId, SessionId? sessionId)
        : base(
            new HookDispatchMetadata(
                new HookPointId("test.hook.point"),
                new HookDispatchId(Guid.NewGuid()),
                new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch + TimeSpan.FromMinutes(1)),
            agentId,
            sessionId)
    {
    }
}
