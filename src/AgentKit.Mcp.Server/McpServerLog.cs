// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

/// <summary>Defines allocation-efficient MCP server events without tool arguments, results, or protocol payload content.</summary>
/// <remarks>This package owns event IDs 13200 through 13299.</remarks>
internal static partial class McpServerLog
{
    /// <summary>Records one inbound MCP request that reached a terminal response.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="operation">The bounded MCP operation label.</param>
    /// <param name="outcome">The normalized terminal response outcome.</param>
    [LoggerMessage(13200, LogLevel.Debug, "MCP server {ServerKey} request {Operation} completed with outcome {Outcome}.")]
    internal static partial void RequestCompleted(ILogger logger, string serverKey, string operation, string outcome);

    /// <summary>Records caller or peer cancellation of one inbound MCP request.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="operation">The bounded MCP operation label.</param>
    [LoggerMessage(13201, LogLevel.Debug, "MCP server {ServerKey} request {Operation} was cancelled.")]
    internal static partial void RequestCancelled(ILogger logger, string serverKey, string operation);

    /// <summary>Records an unexpected content-free failure while serving one inbound MCP request.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="operation">The bounded MCP operation label.</param>
    /// <param name="errorType">The exception type raised while serving the request.</param>
    [LoggerMessage(13202, LogLevel.Error, "MCP server {ServerKey} request {Operation} failed with error type {ErrorType}.")]
    internal static partial void RequestFailed(ILogger logger, string serverKey, string operation, string errorType);

    /// <summary>Records one inbound MCP tool call that reached a terminal result.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="toolName">The bounded tool name the peer requested.</param>
    /// <param name="outcome">The normalized terminal result outcome.</param>
    [LoggerMessage(13210, LogLevel.Debug, "MCP server {ServerKey} tool call {ToolName} completed with outcome {Outcome}.")]
    internal static partial void ToolCallCompleted(ILogger logger, string serverKey, string toolName, string outcome);

    /// <summary>Records cancellation of one inbound MCP tool call.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="toolName">The bounded tool name the peer requested.</param>
    [LoggerMessage(13211, LogLevel.Debug, "MCP server {ServerKey} tool call {ToolName} was cancelled.")]
    internal static partial void ToolCallCancelled(ILogger logger, string serverKey, string toolName);

    /// <summary>Records an unexpected content-free failure while serving one inbound MCP tool call.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="serverKey">The configured server key.</param>
    /// <param name="toolName">The bounded tool name the peer requested.</param>
    /// <param name="errorType">The exception type raised while serving the tool call.</param>
    [LoggerMessage(13212, LogLevel.Error, "MCP server {ServerKey} tool call {ToolName} failed with error type {ErrorType}.")]
    internal static partial void ToolCallFailed(ILogger logger, string serverKey, string toolName, string errorType);
}
