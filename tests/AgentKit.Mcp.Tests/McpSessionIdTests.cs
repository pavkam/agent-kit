// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpSessionId"/>.</summary>
public sealed class McpSessionIdTests: GuidIdentityConformanceTests<McpSessionId>
{
    /// <inheritdoc/>
    protected override McpSessionId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(McpSessionId subject) => subject.Value;
}
