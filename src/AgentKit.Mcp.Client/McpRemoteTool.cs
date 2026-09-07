// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Represents the bounded identity metadata read from a remote MCP tool listing.</summary>
internal sealed record McpRemoteTool
{
    /// <summary>Initializes remote tool identity metadata.</summary>
    /// <param name="name">The protocol-facing tool name.</param>
    /// <param name="version">The optional AgentKit tool contract version.</param>
    public McpRemoteTool(McpToolName name, ToolVersion? version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name.Value, nameof(name));
        if (version is { } presentVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(presentVersion.Value, nameof(version));
        }

        Name = name;
        Version = version;
    }

    /// <summary>Gets the protocol-facing tool name.</summary>
    public McpToolName Name { get; }

    /// <summary>Gets the advertised AgentKit tool contract version, when present.</summary>
    public ToolVersion? Version { get; }
}
