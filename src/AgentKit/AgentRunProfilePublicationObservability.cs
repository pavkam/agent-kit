// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records failure-isolated logs and bounded metrics for exact run-profile publication reads.</summary>
internal static class AgentRunProfilePublicationObservability
{
    private static readonly NonBlockingInstrument<Counter<long>> _reads = new();

    /// <summary>Records one terminal read without allowing diagnostics to alter the semantic outcome.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="agentId">The addressed agent.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    internal static void Record(ILogger logger, AgentId agentId, string outcome)
    {
        Debug.Assert(logger is not null, "The reader owns a non-null logger.");
        Debug.Assert(outcome is "found" or "unavailable" or "cancelled",
            "Run-profile reads use only bounded outcomes.");
        try
        {
            AgentRunProfilePublicationLog.Completed(logger, agentId, outcome);
            GetCounter()?.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        }
        catch
        {
        }
    }

    private static Counter<long>? GetCounter()
        => _reads.GetOrCreate(static () => AgentKitDiagnostics.Metrics.CreateCounter<long>(
            AgentKitMetricNames.AgentRunProfilePublicationReadCount));
}
