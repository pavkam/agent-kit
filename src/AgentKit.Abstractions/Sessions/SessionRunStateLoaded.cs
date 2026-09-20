// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns exact complete accepted state for recovery, including whether durable abort is already committed.</summary>
public sealed record SessionRunStateLoaded: SessionRunStateResult
{
    /// <summary>Initializes a loaded result.</summary>
    /// <param name="state">The complete immutable accepted state.</param>
    /// <param name="abortRequested">
    /// Whether a durable cancel marker is committed for <paramref name="state"/>. Defaults to
    /// <see langword="false"/> so existing recovery callers keep the pre-abort meaning.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    /// <remarks>
    /// <paramref name="abortRequested"/> is not stored on <see cref="SessionAcceptedRunState"/>.
    /// <see langword="true"/> means a durable cancel marker is committed for that accepted run and pending
    /// admissions for that run were pruned in the same commit. Callers must not treat a default
    /// <see langword="false"/> as evidence that abort was considered and declined; it is the value used when
    /// no marker has been loaded.
    /// </remarks>
    public SessionRunStateLoaded(SessionAcceptedRunState state, bool abortRequested = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        AbortRequested = abortRequested;
    }

    /// <summary>Gets accepted state.</summary>
    /// <value>The exact retained state; no current configuration has been substituted.</value>
    public SessionAcceptedRunState State { get; }

    /// <summary>Gets whether durable abort is committed for <see cref="State"/>.</summary>
    /// <value>
    /// <see langword="true"/> when a durable cancel marker is committed for that accepted run and pending
    /// admissions for that run were pruned in the same commit; otherwise <see langword="false"/>.
    /// </value>
    public bool AbortRequested { get; }
}
