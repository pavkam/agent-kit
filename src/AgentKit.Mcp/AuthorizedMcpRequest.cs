// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Binds one typed MCP request to the grant that authorizes its effect.</summary>
public sealed record AuthorizedMcpRequest
{
    /// <summary>Initializes an authorized MCP request.</summary>
    /// <param name="request">The typed MCP request to invoke.</param>
    /// <param name="grant">The bounded grant authorizing this exact request.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public AuthorizedMcpRequest(McpRequest request, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(grant);
        Request = request;
        Grant = grant;
    }

    /// <summary>Gets the typed MCP request.</summary>
    public McpRequest Request { get; }

    /// <summary>Gets the bounded grant authorizing the request.</summary>
    public SecurityGrant Grant { get; }
}
