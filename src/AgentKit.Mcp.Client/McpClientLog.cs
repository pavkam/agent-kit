// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Defines allocation-efficient MCP client events without protocol payload content.</summary>
internal static partial class McpClientLog
{
    /// <summary>Records successful connection and protocol negotiation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="protocolVersion">The negotiated MCP protocol revision.</param>
    [LoggerMessage(13000, LogLevel.Information, "Connected MCP client using protocol version {ProtocolVersion}.")]
    internal static partial void Connected(ILogger logger, McpProtocolVersion protocolVersion);

    /// <summary>Records caller cancellation during MCP connection.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    [LoggerMessage(13001, LogLevel.Debug, "MCP client connection was cancelled.")]
    internal static partial void ConnectCancelled(ILogger logger);

    /// <summary>Records a content-free MCP connection failure.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="errorType">The exception type raised by connection or negotiation.</param>
    [LoggerMessage(13002, LogLevel.Error, "MCP client connection failed with error type {ErrorType}.")]
    internal static partial void ConnectFailed(ILogger logger, string errorType);

    /// <summary>Records publication of one validated immutable MCP catalog generation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="protocolVersion">The negotiated MCP protocol revision.</param>
    /// <param name="catalogVersion">The newly published catalog generation.</param>
    /// <param name="toolCount">The bounded number of reflected contract tools.</param>
    [LoggerMessage(13010, LogLevel.Debug, "Published MCP protocol {ProtocolVersion} catalog generation {CatalogVersion} with {ToolCount} matched tools.")]
    internal static partial void CatalogPublished(
        ILogger logger,
        McpProtocolVersion protocolVersion,
        McpCatalogVersion catalogVersion,
        int toolCount);

    /// <summary>Records caller cancellation during an MCP catalog refresh.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="protocolVersion">The negotiated MCP protocol revision.</param>
    [LoggerMessage(13011, LogLevel.Debug, "MCP protocol {ProtocolVersion} catalog refresh was cancelled.")]
    internal static partial void CatalogRefreshCancelled(ILogger logger, McpProtocolVersion protocolVersion);

    /// <summary>Records a content-free MCP catalog refresh failure.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="protocolVersion">The negotiated MCP protocol revision.</param>
    /// <param name="errorType">The exception type raised during listing or validation.</param>
    [LoggerMessage(13012, LogLevel.Error, "MCP protocol {ProtocolVersion} catalog refresh failed with error type {ErrorType}.")]
    internal static partial void CatalogRefreshFailed(
        ILogger logger,
        McpProtocolVersion protocolVersion,
        string errorType);

    /// <summary>Records successful completion of one remote MCP tool invocation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="toolName">The reflected MCP tool identity.</param>
    /// <param name="catalogVersion">The captured immutable catalog generation.</param>
    [LoggerMessage(13020, LogLevel.Debug, "Completed MCP tool {ToolName} using catalog generation {CatalogVersion}.")]
    internal static partial void ToolCallCompleted(
        ILogger logger,
        McpToolName toolName,
        McpCatalogVersion catalogVersion);

    /// <summary>Records caller cancellation of one remote MCP tool invocation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="toolName">The reflected MCP tool identity.</param>
    /// <param name="catalogVersion">The captured immutable catalog generation.</param>
    [LoggerMessage(13021, LogLevel.Debug, "MCP tool {ToolName} using catalog generation {CatalogVersion} was cancelled.")]
    internal static partial void ToolCallCancelled(
        ILogger logger,
        McpToolName toolName,
        McpCatalogVersion catalogVersion);

    /// <summary>Records a content-free remote MCP tool invocation failure.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="toolName">The reflected MCP tool identity.</param>
    /// <param name="catalogVersion">The captured immutable catalog generation.</param>
    /// <param name="errorType">The exception type raised by transport, protocol, or deserialization.</param>
    [LoggerMessage(13022, LogLevel.Error, "MCP tool {ToolName} using catalog generation {CatalogVersion} failed with error type {ErrorType}.")]
    internal static partial void ToolCallFailed(
        ILogger logger,
        McpToolName toolName,
        McpCatalogVersion catalogVersion,
        string errorType);
}
