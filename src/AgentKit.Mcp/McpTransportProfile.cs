// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Identifies which protected transport an MCP endpoint uses.</summary>
/// <remarks>
/// The set is closed: stdio and HTTP. Adding a transport requires a new
/// derived record and its own process or network grant. A transport profile
/// does not authorize the child process or the HTTP request.
/// </remarks>
public abstract record McpTransportProfile
{
    private protected McpTransportProfile()
    {
    }
}
