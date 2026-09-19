// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpEndpointKey"/>.</summary>
public sealed class McpEndpointKeyTests: StringIdentityConformanceTests<McpEndpointKey>
{
    /// <inheritdoc/>
    protected override McpEndpointKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(McpEndpointKey subject) => subject.Value;
}
