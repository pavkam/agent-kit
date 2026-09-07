// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines content-free structured logs for terminal agent-admission outcomes.</summary>
internal static partial class AgentAdmissionLog
{
    /// <summary>Writes a bounded terminal admission outcome.</summary>
    /// <param name="logger">The destination logger.</param>
    /// <param name="outcome">The normalized outcome.</param>
    [LoggerMessage(18000, LogLevel.Debug, "Agent admission completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, string outcome);

    /// <summary>Writes a bounded admission-cancellation event.</summary>
    /// <param name="logger">The destination logger.</param>
    [LoggerMessage(18001, LogLevel.Debug, "Agent admission was cancelled.")]
    internal static partial void Cancelled(ILogger logger);

    /// <summary>Writes a bounded admission-failure event.</summary>
    /// <param name="logger">The destination logger.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(18002, LogLevel.Error, "Agent admission failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string errorType);
}
