// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpServerKey"/>.</summary>
public sealed class McpServerKeyTests: StringIdentityConformanceTests<McpServerKey>
{
    /// <inheritdoc/>
    protected override McpServerKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(McpServerKey subject) => subject.Value;
}
