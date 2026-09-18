// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines content-free structured logs for terminal agent-admission outcomes.</summary>
internal static partial class AgentAdmissionLog
{
    /// <summary>Writes a bounded terminal admission outcome.</summary>
    /// <param name="logger">The destination logger.</param>
    /// <param name="outcome">The normalized outcome.</param>
    [LoggerMessage(18000, LogLevel.Debug, "Agent admission completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, string outcome);

    /// <summary>Writes a bounded admission-cancellation event.</summary>
    /// <param name="logger">The destination logger.</param>
    [LoggerMessage(18001, LogLevel.Debug, "Agent admission was cancelled.")]
    internal static partial void Cancelled(ILogger logger);

    /// <summary>Writes a bounded admission-failure event.</summary>
    /// <param name="logger">The destination logger.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(18002, LogLevel.Error, "Agent admission failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string errorType);

    /// <summary>Logs that an engine-admitted turn created a new session for the agent.</summary>
    [LoggerMessage(18003, LogLevel.Information, "Agent {AgentId} created session {SessionId} for an admitted turn.")]
    internal static partial void SessionCreated(ILogger logger, AgentId agentId, SessionId sessionId);

    /// <summary>Logs that a requested session was not visible to the requesting agent and identity.</summary>
    [LoggerMessage(18004, LogLevel.Warning, "Agent {AgentId} rejected a turn: session {SessionId} is unavailable or not visible to the requesting identity.")]
    internal static partial void SessionNotVisible(ILogger logger, AgentId agentId, SessionId sessionId);

    /// <summary>Logs that releasing a durably admitted run's lane was rejected.</summary>
    [LoggerMessage(18005, LogLevel.Warning, "Agent {AgentId} session {SessionId}: releasing the admitted lane was rejected ({Outcome}); the lane was not released.")]
    internal static partial void LaneReleaseRejected(ILogger logger, AgentId agentId, SessionId sessionId, string outcome);

    /// <summary>Logs that releasing a durably admitted run's lane faulted.</summary>
    [LoggerMessage(18006, LogLevel.Error, "Agent {AgentId} session {SessionId}: releasing the admitted lane faulted with {ErrorType}; the lane was not released.")]
    internal static partial void LaneReleaseFaulted(ILogger logger, AgentId agentId, SessionId sessionId, string errorType);
}
