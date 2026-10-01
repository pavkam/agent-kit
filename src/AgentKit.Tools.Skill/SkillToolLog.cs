// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill;

/// <summary>Defines allocation-efficient SkillTool events without tool arguments, paths, results, or exception messages.</summary>
/// <remarks>This package owns event IDs 34100 through 34199.</remarks>
internal static partial class SkillToolLog
{
    /// <summary>Records one terminal invocation outcome.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="toolId">The stable tool identity.</param>
    /// <param name="callId">The model-requested call identity.</param>
    /// <param name="outcome">The bounded terminal outcome label.</param>
    [LoggerMessage(34100, LogLevel.Debug, "Tool {ToolId} call {CallId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, ToolId toolId, ToolCallId callId, string outcome);

    /// <summary>Records caller cancellation of one invocation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="toolId">The stable tool identity.</param>
    /// <param name="callId">The model-requested call identity.</param>
    [LoggerMessage(34101, LogLevel.Debug, "Tool {ToolId} call {CallId} was cancelled.")]
    internal static partial void Cancelled(ILogger logger, ToolId toolId, ToolCallId callId);

    /// <summary>Records an unexpected content-free invocation failure.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="toolId">The stable tool identity.</param>
    /// <param name="callId">The model-requested call identity.</param>
    /// <param name="errorType">The exception type raised by the invocation.</param>
    [LoggerMessage(34102, LogLevel.Error, "Tool {ToolId} call {CallId} failed with error type {ErrorType}.")]
    internal static partial void Faulted(ILogger logger, ToolId toolId, ToolCallId callId, string errorType);
}
