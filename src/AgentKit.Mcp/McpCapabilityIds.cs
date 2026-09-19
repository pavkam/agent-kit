// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Stable capability identifiers owned by the MCP package.</summary>
/// <remarks>
/// Agent definitions reference <see cref="Client"/> through a neutral
/// <see cref="AgentCapabilityReference"/>. Endpoint keys stay inside MCP
/// profiles and never appear on the definition.
/// </remarks>
public static class McpCapabilityIds
{
    /// <summary>Gets the capability id for an MCP client profile.</summary>
    public static CapabilityId Client { get; } = new("agentkit.mcp.client");
}
