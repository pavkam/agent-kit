// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

internal sealed class GuidMcpRequestIdGenerator: IIdentifierGenerator<McpRequestId>
{
    /// <inheritdoc/>
    public McpRequestId Create() => new(Guid.NewGuid());
}
