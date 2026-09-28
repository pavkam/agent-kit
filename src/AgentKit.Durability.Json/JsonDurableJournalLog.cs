// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json;

/// <summary>Defines stable content-free structured log events for the JSON durable journal.</summary>
/// <remarks>
/// Every template carries only bounded classifications. The root path, operation payloads, and terminal outputs are
/// content and never reach a log.
/// </remarks>
internal static partial class JsonDurableJournalLog
{
    [LoggerMessage(EventId = 28000, Level = LogLevel.Information, Message = "JSON durable journal {Operation} completed with {Outcome}.")]
    internal static partial void WriteCompleted(ILogger logger, string operation, string outcome);

    [LoggerMessage(EventId = 28001, Level = LogLevel.Debug, Message = "JSON durable journal {Operation} was cancelled.")]
    internal static partial void WriteCancelled(ILogger logger, string operation);

    [LoggerMessage(EventId = 28002, Level = LogLevel.Error, Message = "JSON durable journal {Operation} failed with {ErrorType}.")]
    internal static partial void WriteFailed(ILogger logger, string operation, string errorType);

    [LoggerMessage(EventId = 28003, Level = LogLevel.Information, Message = "JSON durable journal evidence load completed with {Outcome}.")]
    internal static partial void EvidenceLoadCompleted(ILogger logger, string outcome);

    [LoggerMessage(EventId = 28004, Level = LogLevel.Debug, Message = "JSON durable journal evidence load was cancelled.")]
    internal static partial void EvidenceLoadCancelled(ILogger logger);

    [LoggerMessage(EventId = 28005, Level = LogLevel.Warning, Message = "JSON durable journal {Operation} was denied before any record changed.")]
    internal static partial void WriteDenied(ILogger logger, string operation);

    [LoggerMessage(EventId = 28006, Level = LogLevel.Warning, Message = "JSON durable journal recovered an incomplete trailing record during initialization.")]
    internal static partial void RecoveredTornAppend(ILogger logger);

    [LoggerMessage(EventId = 28007, Level = LogLevel.Information, Message = "JSON durable journal compacted {ReplayedRecords} replayed records into {LiveOperations} live operations.")]
    internal static partial void Compacted(ILogger logger, int replayedRecords, int liveOperations);
}
