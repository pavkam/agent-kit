// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Mutable MCP client mechanics configured at composition time.</summary>
/// <remarks>
/// These options bound handshake, request, and shutdown behavior for every
/// endpoint registered while this snapshot is active. They do not select
/// endpoints, credentials, or commands.
/// </remarks>
public sealed class McpClientOptions
{
    /// <summary>Gets or sets the protocol initialization timeout.</summary>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Gets or sets the per-request timeout.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets the session shutdown timeout.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the maximum transport frame size in bytes.</summary>
    public int MaximumFrameBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum protocol message size in bytes.</summary>
    public int MaximumMessageBytes { get; set; } = 4_194_304;

    /// <summary>Gets or sets the maximum number of concurrent correlated requests.</summary>
    public int MaximumInFlightRequests { get; set; } = 16;

    /// <summary>Gets or sets the unknown-notification handling policy.</summary>
    public McpUnknownNotificationPolicy UnknownNotificationPolicy { get; set; } =
        McpUnknownNotificationPolicy.IgnoreAndDiagnose;

    /// <summary>Gets or sets the process executor profile used for stdio MCP servers.</summary>
    public ProcessExecutorKey StdioProcessExecutorKey { get; set; } = new("default");

    /// <summary>Gets or sets the sandbox profile applied when launching stdio MCP servers.</summary>
    public SandboxProfileId StdioSandboxProfileId { get; set; } = new("workspace-no-network-v1");
}
