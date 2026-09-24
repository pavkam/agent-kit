// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Configures one stdio MCP endpoint before it is captured as an immutable <see cref="McpEndpoint"/>.</summary>
public sealed class McpStdioEndpointOptions
{
    /// <summary>Gets or sets the command used to launch the MCP server process.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Gets the command arguments in order.</summary>
    public IList<string> Arguments { get; } = [];
}
