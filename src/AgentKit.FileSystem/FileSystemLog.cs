// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Defines allocation-efficient host file-system events without raw paths or payload content.</summary>
internal static partial class FileSystemLog
{
    /// <summary>Records one terminal host operation with its originating security request when available.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="operation">The bounded file-system operation name.</param>
    /// <param name="securityRequestId">The security request authorizing the effect, when one operation owns a single grant.</param>
    /// <param name="outcome">The normalized terminal result type or status.</param>
    [LoggerMessage(11000, LogLevel.Debug, "File-system operation {Operation} for security request {SecurityRequestId} completed with outcome {Outcome}.")]
    internal static partial void Completed(
        ILogger logger, string operation, SecurityRequestId? securityRequestId, string outcome);

    /// <summary>Records an unexpected host exception without path, arguments, content, or grant resources.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="operation">The bounded file-system operation name.</param>
    /// <param name="securityRequestId">The security request authorizing the effect, when available.</param>
    /// <param name="errorType">The exception type raised by the host boundary.</param>
    [LoggerMessage(11001, LogLevel.Error, "File-system operation {Operation} for security request {SecurityRequestId} failed with error type {ErrorType}.")]
    internal static partial void Failed(
        ILogger logger, string operation, SecurityRequestId? securityRequestId, string errorType);
}
