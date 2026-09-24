// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Leases one MCP tool invoker without owning the shared session.</summary>
internal sealed class McpToolInvokerLease(
    IToolInvoker invoker,
    ToolDescriptor tool,
    ToolSourceVersion sourceVersion): IToolInvokerLease
{
    /// <inheritdoc/>
    public ToolDescriptor Tool { get; } = tool;

    /// <inheritdoc/>
    public ToolSourceVersion SourceVersion { get; } = sourceVersion;

    /// <inheritdoc/>
    public IToolInvoker Invoker { get; } = invoker;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
