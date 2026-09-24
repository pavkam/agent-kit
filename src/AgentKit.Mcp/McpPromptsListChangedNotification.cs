// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Signals that the remote prompt catalog changed.</summary>
public sealed record McpPromptsListChangedNotification(McpSessionId SessionId): McpNotification(SessionId);
