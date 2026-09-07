// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

/// <summary>Defines whether an MCP server supports compatible revisions or requires one exact revision.</summary>
public sealed record McpServerVersionPolicy
{
    private McpServerVersionPolicy(McpProtocolVersion? requiredVersion) => RequiredVersion = requiredVersion;

    /// <summary>Gets the dual-era compatibility policy supplied by the official SDK.</summary>
    public static McpServerVersionPolicy Compatible { get; } = new((McpProtocolVersion?) null);

    /// <summary>Gets the exact protocol revision required by the server, or null for compatible negotiation.</summary>
    public McpProtocolVersion? RequiredVersion { get; }

    /// <summary>Creates a policy that accepts exactly <paramref name="version"/>.</summary>
    /// <param name="version">The only MCP protocol revision the server will accept.</param>
    /// <returns>An exact-version server policy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is uninitialized.</exception>
    public static McpServerVersionPolicy Require(McpProtocolVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(version.Value, default, nameof(version));
        return new McpServerVersionPolicy(version);
    }
}
