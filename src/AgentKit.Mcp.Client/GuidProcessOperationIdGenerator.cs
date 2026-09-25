// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Creates random <see cref="ProcessOperationId"/> values for MCP stdio process starts.</summary>
internal sealed class GuidProcessOperationIdGenerator: IIdentifierGenerator<ProcessOperationId>
{
    /// <inheritdoc/>
    public ProcessOperationId Create() => new(Guid.NewGuid());
}
