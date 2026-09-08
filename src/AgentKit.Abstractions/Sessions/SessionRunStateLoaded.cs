// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns exact complete accepted state for recovery.</summary>
public sealed record SessionRunStateLoaded: SessionRunStateResult
{
    /// <summary>Initializes a loaded result.</summary><param name="state">The complete immutable state.</param><exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    public SessionRunStateLoaded(SessionAcceptedRunState state) { ArgumentNullException.ThrowIfNull(state); State = state; }
    /// <summary>Gets accepted state.</summary><value>The exact retained state; no current configuration has been substituted.</value>
    public SessionAcceptedRunState State { get; }
}
