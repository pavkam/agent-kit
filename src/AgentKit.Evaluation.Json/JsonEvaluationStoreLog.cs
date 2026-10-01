// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json;

/// <summary>Defines content-free structured logs emitted by the JSON evaluation result-store lifecycle.</summary>
/// <remarks>Event identifiers 36300-36309 are owned by this package.</remarks>
internal static partial class JsonEvaluationStoreLog
{
    /// <summary>Records that initialization recovered an incomplete trailing record.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    [LoggerMessage(EventId = 36300, Level = LogLevel.Warning, Message = "JSON evaluation result store recovered an incomplete trailing record during initialization.")]
    internal static partial void RecoveredTornAppend(ILogger logger);

    /// <summary>Records that initialization replayed the log.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="replayedResults">The number of replayed results.</param>
    [LoggerMessage(EventId = 36301, Level = LogLevel.Information, Message = "JSON evaluation result store replayed {ReplayedResults} results.")]
    internal static partial void Replayed(ILogger logger, int replayedResults);
}
