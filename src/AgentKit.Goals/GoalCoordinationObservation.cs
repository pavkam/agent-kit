// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Swallows instrumentation failures so observation can never change a semantic outcome.</summary>
internal static class GoalCoordinationObservation
{
    /// <summary>Runs one observation and discards any exception it throws.</summary>
    /// <param name="observation">The observation to run.</param>
    internal static void Safe(Action observation)
    {
        Debug.Assert(observation is not null, "An observation delegate is required.");
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Instrumentation is observational: a failing listener, logger, or meter never changes a result.
        }
    }

    /// <summary>Reads a timestamp from the injected clock, or null when the clock fails.</summary>
    /// <param name="time">The clock.</param>
    /// <returns>The timestamp, or <see langword="null"/>.</returns>
    internal static long? TryTimestamp(TimeProvider time)
    {
        try { return time.GetTimestamp(); } catch (Exception) { return null; }
    }

    /// <summary>Measures elapsed time since a timestamp, or returns null when it cannot be measured.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="started">The start timestamp, or null.</param>
    /// <returns>The elapsed time, or <see langword="null"/>.</returns>
    internal static TimeSpan? TryElapsed(TimeProvider time, long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try { return time.GetElapsedTime(timestamp); } catch (Exception) { return null; }
    }
}
