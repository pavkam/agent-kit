// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Carries one remote MCP logging message.</summary>
public sealed record McpLoggingMessageNotification: McpNotification
{
    /// <summary>Initializes a logging notification.</summary>
    /// <param name="sessionId">The session that received the notification.</param>
    /// <param name="level">The reported log level.</param>
    /// <param name="message">The non-sensitive log message text.</param>
    /// <param name="loggerName">The optional logger name supplied by the peer.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is uninitialized.</exception>
    public McpLoggingMessageNotification(
        McpSessionId sessionId,
        string level,
        string message,
        string? loggerName = null)
        : base(sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level, nameof(level));
        ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(message));
        Level = level;
        Message = message;
        LoggerName = loggerName;
    }

    /// <summary>Gets the reported log level.</summary>
    public string Level { get; }

    /// <summary>Gets the log message text.</summary>
    public string Message { get; }

    /// <summary>Gets the optional logger name.</summary>
    public string? LoggerName { get; }
}
