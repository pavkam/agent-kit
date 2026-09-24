// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Opens one MCP transport for a declared transport profile kind.</summary>
public interface IMcpTransportFactory
{
    /// <summary>Gets the transport profile type this factory supports.</summary>
    public Type TransportProfileType { get; }

    /// <summary>Opens one transport under the process or network boundary.</summary>
    /// <param name="request">The captured transport open request.</param>
    /// <param name="cancellationToken">A token that cancels opening.</param>
    /// <returns>The terminal transport open outcome.</returns>
    public ValueTask<McpTransportOpenResult> OpenAsync(
        McpTransportOpenRequest request,
        CancellationToken cancellationToken);
}
