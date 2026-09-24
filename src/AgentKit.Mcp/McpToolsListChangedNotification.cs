// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Signals that the remote tool catalog changed.</summary>
public sealed record McpToolsListChangedNotification(McpSessionId SessionId): McpNotification(SessionId);
