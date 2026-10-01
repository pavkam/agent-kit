// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Mutable MCP client mechanics configured at composition time.</summary>
/// <remarks>
/// These options bound handshake, request, and shutdown behavior for every
/// endpoint registered while this snapshot is active, plus the streamed-response
/// bounds and data classification the HTTP transport declares to the network
/// boundary. They do not select endpoints, credentials, or commands.
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

    /// <summary>Gets or sets the maximum lifetime of one server-initiated HTTP response stream.</summary>
    /// <value>
    /// A positive duration. It bounds the standalone <c>GET</c> event stream a Streamable HTTP or legacy SSE session
    /// keeps open; when it elapses the stream faults and the protocol client's own reconnection resumes it. Responses
    /// to client <c>POST</c> and <c>DELETE</c> requests are bounded by <see cref="RequestTimeout"/> instead.
    /// </value>
    public TimeSpan HttpStreamTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the maximum number of response bytes one HTTP exchange may stream.</summary>
    /// <value>
    /// A positive byte count, not less than <see cref="MaximumMessageBytes"/>, enforced by the network boundary while
    /// the body streams. An exchange that exceeds it faults instead of surfacing truncated content.
    /// </value>
    public long MaximumHttpResponseBytes { get; set; } = 16L * 1_048_576;

    /// <summary>Gets or sets the data classification declared for every outbound HTTP MCP request.</summary>
    /// <value>A defined classification; the default is <see cref="NetworkDataClassification.Confidential"/> because requests carry tool arguments and credentials.</value>
    public NetworkDataClassification HttpDataClassification { get; set; } = NetworkDataClassification.Confidential;
}
