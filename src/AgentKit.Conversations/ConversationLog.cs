// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Defines allocation-efficient structured conversation-session log events.</summary>
internal static partial class ConversationLog
{
    /// <summary>Logs the start of one submitted conversational turn without message content.</summary>
    [LoggerMessage(24000, LogLevel.Debug, "Submitting a conversational turn for agent {AgentId}.")]
    internal static partial void TurnStarted(ILogger logger, AgentId agentId);

    /// <summary>Logs that a turn settled with a run outcome, without message content.</summary>
    [LoggerMessage(24001, LogLevel.Debug, "Conversational turn for agent {AgentId} settled with {EventCount} rendered events.")]
    internal static partial void TurnSettled(ILogger logger, AgentId agentId, int eventCount);

    /// <summary>Logs that a turn could not admit the user's message, without message content.</summary>
    [LoggerMessage(24002, LogLevel.Warning, "Conversational turn for agent {AgentId} could not admit the user's message.")]
    internal static partial void TurnAdmissionFailed(ILogger logger, AgentId agentId);

    /// <summary>Logs caller cancellation of a submitted turn.</summary>
    [LoggerMessage(24003, LogLevel.Information, "Conversational turn for agent {AgentId} was cancelled.")]
    internal static partial void TurnCancelled(ILogger logger, AgentId agentId);

    /// <summary>Logs an unexpected turn failure without message content.</summary>
    [LoggerMessage(24004, LogLevel.Error, "Conversational turn for agent {AgentId} faulted with error type {ErrorType}.")]
    internal static partial void TurnFaulted(ILogger logger, AgentId agentId, string errorType);

    /// <summary>Logs lazy creation of this session's underlying durable session record.</summary>
    [LoggerMessage(24005, LogLevel.Debug, "Creating the underlying session for agent {AgentId}.")]
    internal static partial void SessionCreating(ILogger logger, AgentId agentId);

    /// <summary>Logs an unexpected bounded history-read failure without stored content or exception text.</summary>
    [LoggerMessage(24006, LogLevel.Warning, "Conversation history read for agent {AgentId} faulted with error type {ErrorType}.")]
    internal static partial void HistoryReadFaulted(ILogger logger, AgentId agentId, string errorType);
}
