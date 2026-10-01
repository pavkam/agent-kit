// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Swallows instrumentation failures so observation can never change a worker outcome.</summary>
internal static class WorkerObservation
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
}
