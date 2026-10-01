// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports how each required run-event sink fared while engine shutdown drained them.</summary>
/// <remarks>
/// The result is evidence for shutdown diagnostics. A sink listed as timed out or failed may still hold accepted but
/// undelivered events; draining never rewrites an accepted event as persisted and never throws on a sink's behalf.
/// </remarks>
public sealed record RequiredRunEventSinkDrainResult
{
    /// <summary>Initializes drain evidence from the sink names bucketed by outcome.</summary>
    /// <param name="drained">Names of required sinks that drained or had nothing buffered.</param>
    /// <param name="timedOut">Names of required sinks that did not drain within their flush deadline.</param>
    /// <param name="failed">Names of required sinks whose flush faulted.</param>
    /// <exception cref="ArgumentException">An array is default.</exception>
    public RequiredRunEventSinkDrainResult(
        ImmutableArray<string> drained,
        ImmutableArray<string> timedOut,
        ImmutableArray<string> failed)
    {
        ArgumentException.ThrowIfDefault(drained);
        ArgumentException.ThrowIfDefault(timedOut);
        ArgumentException.ThrowIfDefault(failed);
        Drained = drained;
        TimedOut = timedOut;
        Failed = failed;
    }

    /// <summary>Gets an empty result for a composition with no required sinks.</summary>
    public static RequiredRunEventSinkDrainResult Empty { get; } = new([], [], []);

    /// <summary>Gets the names of required sinks that drained or had nothing buffered.</summary>
    public ImmutableArray<string> Drained { get; }

    /// <summary>Gets the names of required sinks that did not drain within their flush deadline.</summary>
    public ImmutableArray<string> TimedOut { get; }

    /// <summary>Gets the names of required sinks whose flush faulted.</summary>
    public ImmutableArray<string> Failed { get; }

    /// <summary>Gets whether every required sink drained.</summary>
    public bool IsClean => TimedOut.IsEmpty && Failed.IsEmpty;
}
