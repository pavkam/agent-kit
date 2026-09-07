// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Defines how a client selects and constrains the MCP protocol revision.</summary>
/// <remarks>
/// Automatic mode lets the official SDK prefer the current modern revision and
/// fall back to a mutually supported legacy revision. A minimum pins the SDK's
/// requested floor and prevents silent downgrade below that revision.
/// </remarks>
public sealed record McpClientVersionPolicy
{
    private McpClientVersionPolicy(McpProtocolVersion? minimumVersion) => MinimumVersion = minimumVersion;

    /// <summary>Gets the automatic modern-first policy with legacy fallback.</summary>
    public static McpClientVersionPolicy Automatic { get; } = new((McpProtocolVersion?) null);

    /// <summary>Gets the minimum acceptable protocol revision, or null for automatic negotiation.</summary>
    public McpProtocolVersion? MinimumVersion { get; }

    /// <summary>Creates a policy that refuses a negotiated revision below <paramref name="version"/>.</summary>
    /// <param name="version">The minimum acceptable MCP protocol revision.</param>
    /// <returns>A minimum-version policy.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is uninitialized.</exception>
    public static McpClientVersionPolicy RequireAtLeast(McpProtocolVersion version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(version.Value, default, nameof(version));
        return new McpClientVersionPolicy(version);
    }
}
