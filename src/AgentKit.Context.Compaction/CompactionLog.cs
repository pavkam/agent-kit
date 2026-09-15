// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Defines allocation-efficient compaction log events without source or checkpoint content.</summary>
internal static partial class CompactionLog
{
    /// <summary>Records the start of a compaction attempt without source or checkpoint content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source is being compacted.</param>
    [LoggerMessage(9000, LogLevel.Debug, "Compaction {CompactionId} started for session {SessionId}.")]
    internal static partial void Started(ILogger logger, CompactionId compactionId, SessionId sessionId);

    /// <summary>Records the typed terminal outcome of a compaction attempt.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was processed.</param>
    /// <param name="outcome">The normalized terminal outcome.</param>
    [LoggerMessage(9001, LogLevel.Information, "Compaction {CompactionId} for session {SessionId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, CompactionId compactionId, SessionId sessionId, string outcome);

    /// <summary>Records caller cancellation of a compaction attempt together with the reconciled commit state.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was being processed.</param>
    /// <param name="commitState">What reconciliation established about the activation append.</param>
    [LoggerMessage(9002, LogLevel.Debug, "Compaction {CompactionId} for session {SessionId} was cancelled with commit state {CommitState}.")]
    internal static partial void Cancelled(ILogger logger, CompactionId compactionId, SessionId sessionId, CompactionCommitState commitState);

    /// <summary>Records an unexpected compaction exception without source or candidate content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was being processed.</param>
    /// <param name="errorType">The exception type raised by the compaction coordinator.</param>
    [LoggerMessage(9003, LogLevel.Error, "Compaction {CompactionId} for session {SessionId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, CompactionId compactionId, SessionId sessionId, string errorType);
}
