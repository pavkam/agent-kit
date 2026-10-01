// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

internal sealed record ForeignToolEvent: ToolEvent
{
    internal ForeignToolEvent(AgentId agentId, SessionId sessionId, RunId runId, TurnId turnId, OperationId operationId, ToolCallId callId)
        : base(agentId, sessionId, runId, turnId, operationId, callId, DateTimeOffset.UnixEpoch) { }

    internal ForeignToolEvent(ToolEvent original) : base(original) { }
}
