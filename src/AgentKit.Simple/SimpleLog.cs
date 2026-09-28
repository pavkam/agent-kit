// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Defines content-free events for the Simple conversation sugar.</summary>
internal static partial class SimpleLog
{
    /// <summary>Records one terminal Simple ask outcome.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="agentId">The agent identity when known.</param>
    /// <param name="sessionId">The session identity when known.</param>
    /// <param name="outcome">The bounded outcome label.</param>
    [LoggerMessage(27000, LogLevel.Debug, "Simple ask for agent {AgentId} and session {SessionId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, string? agentId, string? sessionId, string outcome);

    /// <summary>Records one unexpected Simple ask failure without message content.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="agentId">The agent identity when known.</param>
    /// <param name="sessionId">The session identity when known.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(27001, LogLevel.Error, "Simple ask for agent {AgentId} and session {SessionId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string? agentId, string? sessionId, string errorType);
}
