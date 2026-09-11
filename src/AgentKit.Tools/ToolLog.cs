// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Defines allocation-efficient structured tool-lifecycle log events.</summary>
internal static partial class ToolLog
{
    /// <summary>Records lookup start using only the captured policy reference.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The requested policy key.</param><param name="policyVersion">The exact requested revision.</param>
    [LoggerMessage(4020, LogLevel.Debug, "Resolving tool-result projection policy {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ProjectionPolicyResolutionStarted(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Records successful exact lookup without copying policy content.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The resolved policy key.</param><param name="policyVersion">The exact resolved revision.</param>
    [LoggerMessage(4021, LogLevel.Debug, "Resolved tool-result projection policy {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ProjectionPolicyResolved(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Records unavailable retained policy content without listing alternative policies.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The unavailable policy key.</param><param name="policyVersion">The exact unavailable revision.</param>
    [LoggerMessage(4022, LogLevel.Warning, "Tool-result projection policy {PolicyKey} at version {PolicyVersion} is unavailable.")]
    internal static partial void ProjectionPolicyUnavailable(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

    /// <summary>Records caller cancellation without converting it to an unavailable policy.</summary>
    /// <param name="logger">The catalog's type-specific logger.</param><param name="policyKey">The requested policy key.</param><param name="policyVersion">The exact requested revision.</param>
    [LoggerMessage(4023, LogLevel.Information, "Cancelled tool-result projection-policy resolution for {PolicyKey} at version {PolicyVersion}.")]
    internal static partial void ProjectionPolicyResolutionCancelled(ILogger logger, ToolResultProjectionPolicyKey policyKey, ToolResultProjectionPolicyVersion policyVersion);

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
