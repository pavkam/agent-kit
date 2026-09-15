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

    /// <summary>
    /// Records that the session store committed the activation append but reported a new version other than the
    /// one the persisted record claims, so the record's <c>ActivatedSessionVersion</c> is false.
    /// </summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose branch received the record.</param>
    /// <param name="expectedVersion">The version the record claims it was activated at.</param>
    /// <param name="reportedVersion">The version the store reported after the append.</param>
    [LoggerMessage(9004, LogLevel.Warning, "Compaction {CompactionId} for session {SessionId} committed a record claiming activated version {ExpectedVersion} but the store reported version {ReportedVersion}.")]
    internal static partial void ActivatedVersionMismatch(
        ILogger logger, CompactionId compactionId, SessionId sessionId, SessionVersion expectedVersion, SessionVersion reportedVersion);

    /// <summary>
    /// Records that the model-backed strategy could not obtain an executable summary model, with the selector's
    /// descriptive reason and no prompt, transcript, or candidate content.
    /// </summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was being processed.</param>
    /// <param name="reason">The normalized, content-free selection failure reason.</param>
    [LoggerMessage(9005, LogLevel.Warning, "Compaction {CompactionId} for session {SessionId} could not select a summary model: {Reason}.")]
    internal static partial void SummaryModelSelectionFailed(ILogger logger, CompactionId compactionId, SessionId sessionId, string reason);

    /// <summary>
    /// Records that one bounded summary request is about to be sent to the selected model, with the transcript's
    /// size and whether it was truncated to the configured ceiling, but never its text.
    /// </summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source is being summarized.</param>
    /// <param name="modelRequestId">The identity of the single provider attempt.</param>
    /// <param name="modelAlias">The catalog alias of the selected summary model.</param>
    /// <param name="inputCharacters">The number of transcript characters sent after bounding.</param>
    /// <param name="inputTruncated">Whether the transcript was truncated to <c>MaximumSummaryInputCharacters</c>.</param>
    [LoggerMessage(9006, LogLevel.Debug, "Compaction {CompactionId} for session {SessionId} sending summary request {ModelRequestId} to model {ModelAlias} with {InputCharacters} transcript characters (truncated: {InputTruncated}).")]
    internal static partial void SummaryModelRequestStarted(
        ILogger logger, CompactionId compactionId, SessionId sessionId, ModelRequestId modelRequestId, ModelAlias modelAlias, int inputCharacters, bool inputTruncated);

    /// <summary>
    /// Records that the summary model returned a usable summary, with its bounded size and whether it was truncated
    /// to the checkpoint ceiling, but never its text.
    /// </summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was summarized.</param>
    /// <param name="modelRequestId">The identity of the single provider attempt.</param>
    /// <param name="outputCharacters">The number of summary characters retained after bounding.</param>
    /// <param name="outputTruncated">Whether the summary was truncated to <c>MaximumCheckpointCharacters</c>.</param>
    [LoggerMessage(9007, LogLevel.Debug, "Compaction {CompactionId} for session {SessionId} summary request {ModelRequestId} produced {OutputCharacters} summary characters (truncated: {OutputTruncated}).")]
    internal static partial void SummaryModelRequestCompleted(
        ILogger logger, CompactionId compactionId, SessionId sessionId, ModelRequestId modelRequestId, int outputCharacters, bool outputTruncated);

    /// <summary>
    /// Records that the summary model attempt ended without a usable summary: a provider failure, a length-limited
    /// or tool-calling response, or a response without text. Carries the normalized reason and no content.
    /// </summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was being summarized.</param>
    /// <param name="modelRequestId">The identity of the single provider attempt.</param>
    /// <param name="reason">The normalized, content-free failure reason.</param>
    [LoggerMessage(9008, LogLevel.Warning, "Compaction {CompactionId} for session {SessionId} summary request {ModelRequestId} failed: {Reason}.")]
    internal static partial void SummaryModelRequestFailed(
        ILogger logger, CompactionId compactionId, SessionId sessionId, ModelRequestId modelRequestId, string reason);

    /// <summary>Records that the caller cancelled the summary model attempt before it produced a summary.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="compactionId">The logical checkpoint identity.</param>
    /// <param name="sessionId">The session whose bounded source was being summarized.</param>
    /// <param name="modelRequestId">The identity of the single provider attempt.</param>
    [LoggerMessage(9009, LogLevel.Debug, "Compaction {CompactionId} for session {SessionId} summary request {ModelRequestId} was cancelled.")]
    internal static partial void SummaryModelRequestCancelled(
        ILogger logger, CompactionId compactionId, SessionId sessionId, ModelRequestId modelRequestId);
}
