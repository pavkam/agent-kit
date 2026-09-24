// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>The immutable base for one inbound MCP notification.</summary>
/// <remarks>
/// Notifications are a closed hierarchy. Optional unknown metadata may be
/// preserved as bounded extension data by concrete adapters without exposing a
/// generic event-name plus object escape hatch.
/// </remarks>
public abstract record McpNotification
{
    /// <summary>Initializes one MCP notification.</summary>
    /// <param name="sessionId">The session that received the notification.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sessionId"/> is default.</exception>
    private protected McpNotification(McpSessionId sessionId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        SessionId = sessionId;
    }

    /// <summary>Gets the session that received the notification.</summary>
    public McpSessionId SessionId { get; }
}
