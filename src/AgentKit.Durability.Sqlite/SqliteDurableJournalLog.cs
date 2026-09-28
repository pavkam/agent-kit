// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Defines stable content-free structured log events for the SQLite durable journal and lease manager.</summary>
/// <remarks>
/// Every template carries only bounded classifications and typed identities. The target path, operation payloads, and
/// terminal outputs are content and never reach a log.
/// </remarks>
internal static partial class SqliteDurableJournalLog
{
    [LoggerMessage(EventId = 27000, Level = LogLevel.Information, Message = "SQLite durable journal {Operation} completed with {Outcome}.")]
    internal static partial void WriteCompleted(ILogger logger, string operation, string outcome);

    [LoggerMessage(EventId = 27001, Level = LogLevel.Debug, Message = "SQLite durable journal {Operation} was cancelled.")]
    internal static partial void WriteCancelled(ILogger logger, string operation);

    [LoggerMessage(EventId = 27002, Level = LogLevel.Error, Message = "SQLite durable journal {Operation} failed with {ErrorType}.")]
    internal static partial void WriteFailed(ILogger logger, string operation, string errorType);

    [LoggerMessage(EventId = 27003, Level = LogLevel.Information, Message = "SQLite durable journal evidence load completed with {Outcome}.")]
    internal static partial void EvidenceLoadCompleted(ILogger logger, string outcome);

    [LoggerMessage(EventId = 27004, Level = LogLevel.Debug, Message = "SQLite durable journal evidence load was cancelled.")]
    internal static partial void EvidenceLoadCancelled(ILogger logger);

    [LoggerMessage(EventId = 27005, Level = LogLevel.Warning, Message = "SQLite durable journal {Operation} was denied before any record changed.")]
    internal static partial void WriteDenied(ILogger logger, string operation);

    [LoggerMessage(EventId = 27006, Level = LogLevel.Information, Message = "SQLite execution lease acquisition for worker {WorkerId} completed with {Outcome}.")]
    internal static partial void AcquisitionCompleted(ILogger logger, WorkerId workerId, string outcome);

    [LoggerMessage(EventId = 27007, Level = LogLevel.Debug, Message = "SQLite execution lease acquisition for worker {WorkerId} was cancelled.")]
    internal static partial void AcquisitionCancelled(ILogger logger, WorkerId workerId);

    [LoggerMessage(EventId = 27008, Level = LogLevel.Error, Message = "SQLite execution lease acquisition for worker {WorkerId} failed with {ErrorType}.")]
    internal static partial void AcquisitionFailed(ILogger logger, WorkerId workerId, string errorType);

    [LoggerMessage(EventId = 27009, Level = LogLevel.Information, Message = "SQLite execution lease renewal for worker {WorkerId} completed with {Outcome}.")]
    internal static partial void RenewalCompleted(ILogger logger, WorkerId workerId, string outcome);

    [LoggerMessage(EventId = 27010, Level = LogLevel.Debug, Message = "SQLite execution lease renewal for worker {WorkerId} was cancelled.")]
    internal static partial void RenewalCancelled(ILogger logger, WorkerId workerId);

    [LoggerMessage(EventId = 27011, Level = LogLevel.Error, Message = "SQLite execution lease renewal for worker {WorkerId} failed with {ErrorType}.")]
    internal static partial void RenewalFailed(ILogger logger, WorkerId workerId, string errorType);

    [LoggerMessage(EventId = 27012, Level = LogLevel.Debug, Message = "SQLite execution lease for worker {WorkerId} was released.")]
    internal static partial void Released(ILogger logger, WorkerId workerId);

    [LoggerMessage(EventId = 27013, Level = LogLevel.Error, Message = "SQLite durable store release for worker {WorkerId} failed with {ErrorType}.")]
    internal static partial void ReleaseFailed(ILogger logger, WorkerId workerId, string errorType);
}
