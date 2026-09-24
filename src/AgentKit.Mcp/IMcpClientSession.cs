// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Represents one MCP client connection and its negotiated catalog state.</summary>
/// <remarks>
/// The caller owns the session and must dispose it asynchronously. A session
/// validates lifecycle, capability, identity binding, and grant scope before
/// writing frames.
/// </remarks>
public interface IMcpClientSession: IAsyncDisposable
{
    /// <summary>Gets the stable session identity.</summary>
    public McpSessionId Id { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public McpSessionState State { get; }

    /// <summary>Completes protocol initialization for this connection.</summary>
    /// <param name="cancellationToken">A token that cancels initialization.</param>
    /// <returns>The negotiated initialization result.</returns>
    public ValueTask<McpInitializeResult> InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Gets the current immutable catalog snapshot.</summary>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The current catalog snapshot.</returns>
    public ValueTask<McpCatalogSnapshot> GetCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Invokes one authorized MCP request.</summary>
    /// <param name="request">The authorized request to invoke.</param>
    /// <param name="cancellationToken">A token that cancels the invocation.</param>
    /// <returns>The terminal MCP response.</returns>
    public ValueTask<McpResponse> InvokeAsync(AuthorizedMcpRequest request, CancellationToken cancellationToken);

    /// <summary>Reads inbound MCP notifications for this session.</summary>
    /// <param name="cancellationToken">A token that cancels notification reading.</param>
    /// <returns>The notification stream for this session.</returns>
    public IAsyncEnumerable<McpNotification> ReadNotificationsAsync(CancellationToken cancellationToken);
}
