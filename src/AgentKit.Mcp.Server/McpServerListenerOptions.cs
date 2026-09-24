// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

/// <summary>Configures one registered AgentKit MCP server listener.</summary>
public sealed class McpServerListenerOptions
{
    /// <summary>Gets or sets the listener transport profile.</summary>
    public McpTransportProfile? Transport { get; set; }
}
