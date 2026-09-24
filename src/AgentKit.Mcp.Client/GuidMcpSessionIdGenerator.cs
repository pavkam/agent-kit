// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

internal sealed class GuidMcpSessionIdGenerator: IIdentifierGenerator<McpSessionId>
{
    /// <inheritdoc/>
    public McpSessionId Create() => new(Guid.NewGuid());
}
