// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Defines stable content-free structured durable-journal log events.</summary>
internal static partial class DurableJournalLog
{
    [LoggerMessage(EventId = 21000, Level = LogLevel.Information, Message = "Durable journal {Operation} completed with {Outcome}.")]
    internal static partial void WriteCompleted(ILogger logger, string operation, string outcome);

    [LoggerMessage(EventId = 21001, Level = LogLevel.Debug, Message = "Durable journal {Operation} was cancelled.")]
    internal static partial void WriteCancelled(ILogger logger, string operation);

    [LoggerMessage(EventId = 21002, Level = LogLevel.Error, Message = "Durable journal {Operation} failed with {ErrorType}.")]
    internal static partial void WriteFailed(ILogger logger, string operation, string errorType);

    [LoggerMessage(EventId = 21003, Level = LogLevel.Information, Message = "Durable journal evidence load completed with {Outcome}.")]
    internal static partial void EvidenceLoadCompleted(ILogger logger, string outcome);

    [LoggerMessage(EventId = 21004, Level = LogLevel.Debug, Message = "Durable journal evidence load was cancelled.")]
    internal static partial void EvidenceLoadCancelled(ILogger logger);
}
