// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

/// <summary>Defines content-free structured logs emitted by the JSON memory-storage lifecycle.</summary>
/// <remarks>Event identifiers 32100-32109 are owned by this package.</remarks>
internal static partial class JsonMemoryLog
{
    /// <summary>Records that initialization recovered an incomplete trailing record.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="family">The bounded state family.</param>
    [LoggerMessage(EventId = 32100, Level = LogLevel.Warning, Message = "JSON {Family} store recovered an incomplete trailing record during initialization.")]
    internal static partial void RecoveredTornAppend(ILogger logger, string family);

    /// <summary>Records that initialization compacted the log.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="family">The bounded state family.</param>
    /// <param name="replayedRecords">The number of replayed records.</param>
    /// <param name="liveRecords">The number of records kept.</param>
    [LoggerMessage(EventId = 32101, Level = LogLevel.Information, Message = "JSON {Family} store compacted {ReplayedRecords} replayed records into {LiveRecords} live records.")]
    internal static partial void Compacted(ILogger logger, string family, int replayedRecords, int liveRecords);
}
