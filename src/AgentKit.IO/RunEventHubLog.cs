// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Emits structural hub diagnostics without event payloads or exception messages.</summary>
internal static partial class RunEventHubLog
{
    /// <summary>Records one local fan-out operation and its bounded terminal outcome.</summary>
    /// <param name="logger">The nonnull run-hub logger.</param>
    /// <param name="level">The outcome's semantic severity.</param>
    /// <param name="operation">The bounded operation label.</param>
    /// <param name="outcome">The bounded outcome label.</param>
    /// <param name="agentId">The accepted agent identity.</param>
    /// <param name="sessionId">The accepted session identity.</param>
    /// <param name="runId">The accepted run identity.</param>
    [LoggerMessage(EventId = 1006, Message = "Run-event hub {Operation} ended with {Outcome}. Agent {AgentId} session {SessionId} run {RunId}.")]
    internal static partial void Completed(ILogger logger, LogLevel level, string operation, string outcome, AgentId agentId, SessionId sessionId, RunId runId);
}
