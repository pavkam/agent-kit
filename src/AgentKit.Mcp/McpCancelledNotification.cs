// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Signals that one in-flight MCP request was cancelled by the peer.</summary>
public sealed record McpCancelledNotification: McpNotification
{
    /// <summary>Initializes a cancellation notification.</summary>
    /// <param name="sessionId">The session that received the notification.</param>
    /// <param name="requestId">The MCP request identity that was cancelled.</param>
    public McpCancelledNotification(McpSessionId sessionId, McpRequestId requestId)
        : base(sessionId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        RequestId = requestId;
    }

    /// <summary>Gets the cancelled MCP request identity.</summary>
    public McpRequestId RequestId { get; }
}
