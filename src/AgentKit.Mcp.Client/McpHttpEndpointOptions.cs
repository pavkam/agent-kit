// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Configures one HTTP MCP endpoint before it is captured as an immutable <see cref="McpEndpoint"/>.</summary>
public sealed class McpHttpEndpointOptions
{
    /// <summary>Gets or sets the absolute credential-free HTTP(S) endpoint.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Gets or sets the optional credential profile reference for this endpoint.</summary>
    public McpAuthenticationReference? Authentication { get; set; }
}
