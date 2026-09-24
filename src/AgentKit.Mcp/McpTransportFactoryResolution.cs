// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>The immutable base for one transport-factory lookup outcome.</summary>
public abstract record McpTransportFactoryResolution
{
    /// <summary>Initializes a transport-factory resolution outcome.</summary>
    private protected McpTransportFactoryResolution()
    {
    }
}
