// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>The immutable base for one endpoint catalog lookup outcome.</summary>
public abstract record McpEndpointResolution
{
    /// <summary>Initializes an endpoint resolution outcome.</summary>
    private protected McpEndpointResolution()
    {
    }
}
