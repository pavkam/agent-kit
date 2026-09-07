// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Describes the identity and contract version advertised for one remote MCP tool.</summary>
public sealed record McpRemoteToolDescriptor
{
    /// <summary>Initializes a remote tool descriptor.</summary>
    /// <param name="name">The protocol-facing tool name.</param>
    /// <param name="version">The advertised AgentKit tool contract version.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="version"/> is uninitialized.</exception>
    public McpRemoteToolDescriptor(McpToolName name, ToolVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name.Value, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        Name = name;
        Version = version;
    }

    /// <summary>Gets the protocol-facing tool name.</summary>
    public McpToolName Name { get; }

    /// <summary>Gets the advertised tool contract version.</summary>
    public ToolVersion Version { get; }
}
