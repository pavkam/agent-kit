// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>
/// Reports that a server negotiated an MCP protocol revision below the caller's
/// <see cref="McpClientVersionPolicy.RequireAtLeast"/> floor.
/// </summary>
[Serializable]
public sealed class McpProtocolVersionBelowMinimumException: Exception
{
    /// <summary>Initializes an exception reporting a negotiated revision below the required floor.</summary>
    /// <param name="negotiatedVersion">The protocol revision the server actually negotiated.</param>
    /// <param name="minimumVersion">The minimum protocol revision <see cref="McpClientVersionPolicy.RequireAtLeast"/> required.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="negotiatedVersion"/> or <paramref name="minimumVersion"/> is uninitialized.
    /// </exception>
    public McpProtocolVersionBelowMinimumException(McpProtocolVersion negotiatedVersion, McpProtocolVersion minimumVersion)
        : base($"The server negotiated MCP protocol version '{negotiatedVersion}', which is below the required minimum '{minimumVersion}'.")
    {
        ArgumentOutOfRangeException.ThrowIfEqual(negotiatedVersion.Value, default);
        ArgumentOutOfRangeException.ThrowIfEqual(minimumVersion.Value, default);
        NegotiatedVersion = negotiatedVersion;
        MinimumVersion = minimumVersion;
    }

    /// <summary>Gets the protocol revision the server actually negotiated.</summary>
    public McpProtocolVersion NegotiatedVersion { get; }

    /// <summary>Gets the minimum protocol revision the caller required.</summary>
    public McpProtocolVersion MinimumVersion { get; }
}
