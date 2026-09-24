// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Captures everything required to open one MCP transport.</summary>
public sealed record McpTransportOpenRequest
{
    /// <summary>Initializes a transport open request.</summary>
    /// <param name="sessionOpenRequest">The captured session open request.</param>
    /// <param name="endpoint">The endpoint whose transport profile should open.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoint"/> is not the endpoint captured on <paramref name="sessionOpenRequest"/>.
    /// </exception>
    public McpTransportOpenRequest(McpClientOpenRequest sessionOpenRequest, McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(sessionOpenRequest);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (sessionOpenRequest.Endpoint.Key != endpoint.Key
            || sessionOpenRequest.Endpoint.Revision != endpoint.Revision)
        {
            throw new ArgumentException(
                "The endpoint must match the endpoint captured on the session open request.",
                nameof(endpoint));
        }

        SessionOpenRequest = sessionOpenRequest;
        Endpoint = endpoint;
    }

    /// <summary>Gets the captured session open request.</summary>
    public McpClientOpenRequest SessionOpenRequest { get; }

    /// <summary>Gets the endpoint whose transport should open.</summary>
    public McpEndpoint Endpoint { get; }
}
