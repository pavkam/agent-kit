// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines content-free structured events for engine lifecycle transitions such as shutdown draining.</summary>
/// <remarks>This class owns event IDs 18200 through 18299.</remarks>
internal static partial class EngineLifecycleLog
{
    /// <summary>Reports how required run-event sinks fared at shutdown, never throwing from the logger.</summary>
    /// <param name="logger">The engine logger.</param>
    /// <param name="drain">The drain evidence from the required-sink coordinator.</param>
    internal static void RequiredSinkDrainReported(ILogger logger, RequiredRunEventSinkDrainResult drain)
    {
        try
        {
            if (drain.IsClean)
            {
                RequiredSinksDrained(logger, drain.Drained.Length);
            }
            else
            {
                RequiredSinksNotDrained(logger, string.Join(',', drain.TimedOut), string.Join(',', drain.Failed));
            }
        }
        catch (Exception)
        {
            // Shutdown diagnostics never change the disposal outcome.
        }
    }

    /// <summary>Records that every required run-event sink drained at shutdown.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="sinkCount">The number of required sinks that drained.</param>
    [LoggerMessage(18200, LogLevel.Debug, "Engine shutdown drained {SinkCount} required run-event sinks.")]
    private static partial void RequiredSinksDrained(ILogger logger, int sinkCount);

    /// <summary>Records that required run-event sinks timed out or faulted while draining at shutdown.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="timedOutSinks">The comma-separated names of sinks that missed their flush deadline.</param>
    /// <param name="failedSinks">The comma-separated names of sinks whose flush faulted.</param>
    [LoggerMessage(18201, LogLevel.Warning, "Engine shutdown could not drain required run-event sinks; timed out: [{TimedOutSinks}], failed: [{FailedSinks}]. Accepted events may be undelivered.")]
    private static partial void RequiredSinksNotDrained(ILogger logger, string timedOutSinks, string failedSinks);
}
