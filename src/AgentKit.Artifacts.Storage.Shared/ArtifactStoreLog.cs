// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Defines content-free structured logs emitted by every artifact store adapter.</summary>
/// <remarks>Event identifiers 29100-29119 are owned by the shared artifact store machinery.</remarks>
internal static partial class ArtifactStoreLog
{
    /// <summary>Records a terminal artifact store operation outcome.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">The severity reflecting the semantic outcome.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation.</param>
    /// <param name="outcome">The bounded outcome.</param>
    [LoggerMessage(EventId = 29100, Message = "Artifact store {Adapter} operation {Operation} ended with {Outcome}.")]
    internal static partial void Completed(ILogger logger, LogLevel level, string adapter, string operation, string outcome);

    /// <summary>Records a cancelled artifact store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation.</param>
    [LoggerMessage(EventId = 29101, Level = LogLevel.Information, Message = "Artifact store {Adapter} operation {Operation} was cancelled.")]
    internal static partial void Cancelled(ILogger logger, string adapter, string operation);

    /// <summary>Records an unexpected artifact store exception by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(EventId = 29102, Level = LogLevel.Error, Message = "Artifact store {Adapter} operation {Operation} failed with {ErrorType}.")]
    internal static partial void Faulted(ILogger logger, string adapter, string operation, string errorType);

    /// <summary>Records that a durable write failed, so its commit status is unknown.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(EventId = 29103, Level = LogLevel.Error, Message = "Artifact store {Adapter} operation {Operation} could not persist durably: {ErrorType}.")]
    internal static partial void PersistFailed(ILogger logger, string adapter, string operation, string errorType);

    /// <summary>Records that releasing an unreferenced payload failed after its entry was already persisted.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(EventId = 29104, Level = LogLevel.Warning, Message = "Artifact store {Adapter} could not release an unreferenced payload: {ErrorType}.")]
    internal static partial void ReleaseFailed(ILogger logger, string adapter, string errorType);

    /// <summary>Records that initialization recovered an incomplete trailing record.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    [LoggerMessage(EventId = 29105, Level = LogLevel.Warning, Message = "Artifact store {Adapter} recovered an incomplete trailing record during initialization.")]
    internal static partial void RecoveredTornAppend(ILogger logger, string adapter);

    /// <summary>Records that initialization compacted the durable log.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="replayedRecords">The number of replayed records.</param>
    /// <param name="liveRecords">The number of records kept.</param>
    [LoggerMessage(EventId = 29106, Level = LogLevel.Information, Message = "Artifact store {Adapter} compacted {ReplayedRecords} replayed records into {LiveRecords} live records.")]
    internal static partial void Compacted(ILogger logger, string adapter, int replayedRecords, int liveRecords);
}
