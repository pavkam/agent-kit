// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Captures everything required to open one MCP session without rereading mutable configuration.</summary>
/// <remarks>
/// The endpoint key must already be listed on the capability profile. Opening
/// does not rediscover a current agent or substitute a newer endpoint revision.
/// The operation context is authorization evidence, not a grant; the session
/// still obtains its own MCP grant, and the transport obtains a process or
/// network grant.
/// </remarks>
public sealed record McpClientOpenRequest
{
    /// <summary>Initializes an open request whose endpoint belongs to the profile.</summary>
    /// <param name="capabilityProfile">The captured MCP client profile.</param>
    /// <param name="endpoint">An endpoint whose key is listed on <paramref name="capabilityProfile"/>.</param>
    /// <param name="operation">The protected semantic operation this session serves.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoint"/> is not one of <paramref name="capabilityProfile"/>'s endpoint keys.
    /// </exception>
    public McpClientOpenRequest(
        McpCapabilityProfile capabilityProfile,
        McpEndpoint endpoint,
        ProtectedSemanticOperationContext operation)
    {
        ArgumentNullException.ThrowIfNull(capabilityProfile);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(operation);
        if (!capabilityProfile.EndpointKeys.Contains(endpoint.Key))
        {
            throw new ArgumentException(
                "The endpoint key must be listed on the capability profile.",
                nameof(endpoint));
        }

        CapabilityProfile = capabilityProfile;
        Endpoint = endpoint;
        Operation = operation;
    }

    /// <summary>Gets the captured capability profile.</summary>
    public McpCapabilityProfile CapabilityProfile { get; }

    /// <summary>Gets the captured endpoint.</summary>
    public McpEndpoint Endpoint { get; }

    /// <summary>Gets the protected operation this session is opened for.</summary>
    public ProtectedSemanticOperationContext Operation { get; }
}
