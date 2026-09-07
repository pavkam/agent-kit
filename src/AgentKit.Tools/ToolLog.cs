// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Defines allocation-efficient structured tool-lifecycle log events.</summary>
internal static partial class ToolLog
{
    /// <summary>Logs the start of one identified tool call without its arguments.</summary>
    [LoggerMessage(4000, LogLevel.Debug, "Starting tool call {ToolCallId} for tool {ToolId}.")]
    internal static partial void Started(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs rejection because the requested tool is absent.</summary>
    [LoggerMessage(4001, LogLevel.Warning, "Rejected tool call {ToolCallId} because tool {ToolId} is not registered.")]
    internal static partial void Unknown(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs rejection by the configured authorization policy.</summary>
    [LoggerMessage(4002, LogLevel.Warning, "Denied tool call {ToolCallId} for tool {ToolId}.")]
    internal static partial void Denied(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs the normalized terminal result of an invoked tool.</summary>
    [LoggerMessage(4003, LogLevel.Debug, "Completed tool call {ToolCallId} for tool {ToolId} with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger,
        ToolCallId toolCallId,
        ToolId toolId,
        ToolCallOutcomeKind outcome);

    /// <summary>Logs caller cancellation without copying tool arguments or results.</summary>
    [LoggerMessage(4004, LogLevel.Information, "Cancelled tool call {ToolCallId} for tool {ToolId}.")]
    internal static partial void Cancelled(ILogger logger, ToolCallId toolCallId, ToolId toolId);

    /// <summary>Logs an unexpected implementation exception type without copying its potentially sensitive message.</summary>
    [LoggerMessage(4005, LogLevel.Error, "Tool call {ToolCallId} for tool {ToolId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, ToolCallId toolCallId, ToolId toolId, string errorType);
}
