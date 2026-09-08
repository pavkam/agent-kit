// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines content-free structured logs for exact run-profile publication reads.</summary>
internal static partial class AgentRunProfilePublicationLog
{
    /// <summary>Writes one bounded terminal read outcome.</summary>
    /// <param name="logger">The destination logger.</param>
    /// <param name="agentId">The addressed agent.</param>
    /// <param name="outcome">The bounded found, unavailable, or cancelled outcome.</param>
    [LoggerMessage(18003, LogLevel.Debug,
        "Run-profile publication read for agent {AgentId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, AgentId agentId, string outcome);
}
