// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Captures one immutable, locally versioned view of matching remote tools.</summary>
public sealed record McpClientToolCatalogSnapshot
{
    /// <summary>Initializes an immutable client catalog snapshot.</summary>
    /// <param name="version">The local publication generation.</param>
    /// <param name="protocolVersion">The negotiated wire protocol revision.</param>
    /// <param name="tools">The remote tools matching the local reflected contract.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is less than one, or <paramref name="protocolVersion"/> is uninitialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="tools"/> has its default value.</exception>
    public McpClientToolCatalogSnapshot(
        McpCatalogVersion version,
        McpProtocolVersion protocolVersion,
        ImmutableArray<McpRemoteToolDescriptor> tools)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version.Value, 1, nameof(version));
        ArgumentOutOfRangeException.ThrowIfEqual(protocolVersion.Value, default, nameof(protocolVersion));
        ArgumentNullException.ThrowIfNull(tools.IsDefault ? null : tools, nameof(tools));
        Version = version;
        ProtocolVersion = protocolVersion;
        Tools = tools;
    }

    /// <summary>Gets the local publication generation.</summary>
    public McpCatalogVersion Version { get; }

    /// <summary>Gets the negotiated wire protocol revision.</summary>
    public McpProtocolVersion ProtocolVersion { get; }

    /// <summary>Gets the matching remote tools.</summary>
    public ImmutableArray<McpRemoteToolDescriptor> Tools { get; }
}
