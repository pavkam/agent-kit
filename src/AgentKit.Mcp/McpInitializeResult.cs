// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Captures the negotiated protocol state after session initialization.</summary>
public sealed record McpInitializeResult
{
    /// <summary>Initializes an initialization result.</summary>
    /// <param name="negotiatedVersion">The negotiated protocol revision.</param>
    /// <param name="serverCapabilities">The negotiated server capability view.</param>
    /// <param name="serverName">Optional display metadata supplied by the server.</param>
    /// <param name="serverVersion">Optional display metadata supplied by the server.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="negotiatedVersion"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="serverCapabilities"/> is null.</exception>
    public McpInitializeResult(
        McpProtocolVersion negotiatedVersion,
        McpCapabilitySet serverCapabilities,
        string? serverName = null,
        string? serverVersion = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(negotiatedVersion, default);
        ArgumentNullException.ThrowIfNull(serverCapabilities);
        NegotiatedVersion = negotiatedVersion;
        ServerCapabilities = serverCapabilities;
        ServerName = serverName;
        ServerVersion = serverVersion;
    }

    /// <summary>Gets the negotiated protocol revision.</summary>
    public McpProtocolVersion NegotiatedVersion { get; }

    /// <summary>Gets the negotiated server capability view.</summary>
    public McpCapabilitySet ServerCapabilities { get; }

    /// <summary>Gets optional server display metadata.</summary>
    public string? ServerName { get; }

    /// <summary>Gets optional server version display metadata.</summary>
    public string? ServerVersion { get; }
}
