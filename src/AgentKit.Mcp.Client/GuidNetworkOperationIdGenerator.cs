// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Creates random GUID-backed network-operation identities for HTTP MCP exchanges.</summary>
/// <remarks>This default is registered with <c>TryAdd</c> semantics so hosts and tests replace it through DI.</remarks>
internal sealed class GuidNetworkOperationIdGenerator: IIdentifierGenerator<NetworkOperationId>
{
    /// <inheritdoc/>
    public NetworkOperationId Create() => new(Guid.NewGuid());
}
