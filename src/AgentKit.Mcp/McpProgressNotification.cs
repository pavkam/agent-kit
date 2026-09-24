// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Reports progress for one in-flight MCP operation.</summary>
public sealed record McpProgressNotification: McpNotification
{
    /// <summary>Initializes a progress notification.</summary>
    /// <param name="sessionId">The session that received the notification.</param>
    /// <param name="progressToken">The protocol progress token being updated.</param>
    /// <param name="progress">The reported progress value.</param>
    /// <param name="total">The optional total progress value.</param>
    /// <exception cref="ArgumentException"><paramref name="progressToken"/> is uninitialized.</exception>
    public McpProgressNotification(
        McpSessionId sessionId,
        string progressToken,
        double progress,
        double? total = null)
        : base(sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(progressToken, nameof(progressToken));
        ProgressToken = progressToken;
        Progress = progress;
        Total = total;
    }

    /// <summary>Gets the protocol progress token.</summary>
    public string ProgressToken { get; }

    /// <summary>Gets the reported progress value.</summary>
    public double Progress { get; }

    /// <summary>Gets the optional total progress value.</summary>
    public double? Total { get; }
}
