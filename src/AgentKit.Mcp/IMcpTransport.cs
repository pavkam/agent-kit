// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Represents one opened MCP transport owned by the session or factory.</summary>
/// <remarks>
/// This neutral boundary keeps protocol SDK types out of AgentKit.Mcp. Leaf
/// transport factories in AgentKit.Mcp.Client map process or network handles
/// to concrete implementations of this contract.
/// </remarks>
public interface IMcpTransport: IAsyncDisposable
{
}
