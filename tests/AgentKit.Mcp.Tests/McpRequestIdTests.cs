// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpRequestId"/>.</summary>
public sealed class McpRequestIdTests: GuidIdentityConformanceTests<McpRequestId>
{
    /// <inheritdoc/>
    protected override McpRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(McpRequestId subject) => subject.Value;
}
