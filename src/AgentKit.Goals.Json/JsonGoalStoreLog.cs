// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json;

/// <summary>Defines content-free structured logs emitted by the JSON goal-store lifecycle.</summary>
/// <remarks>Event identifiers 31300-31309 are owned by this package.</remarks>
internal static partial class JsonGoalStoreLog
{
    /// <summary>Records that initialization recovered an incomplete trailing record.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    [LoggerMessage(EventId = 31300, Level = LogLevel.Warning, Message = "JSON goal store recovered an incomplete trailing record during initialization.")]
    internal static partial void RecoveredTornAppend(ILogger logger);

    /// <summary>Records that initialization compacted the log.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="replayedRecords">The number of replayed records.</param>
    /// <param name="liveGoals">The number of goals kept.</param>
    [LoggerMessage(EventId = 31301, Level = LogLevel.Information, Message = "JSON goal store compacted {ReplayedRecords} replayed records into {LiveGoals} live goals.")]
    internal static partial void Compacted(ILogger logger, int replayedRecords, int liveGoals);
}
