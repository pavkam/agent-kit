// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The session already has an active mutating run and the configured busy
/// behavior rejected this request rather than queuing or waiting for it.
/// </summary>
public sealed record SessionRunBusy: SessionRunLeaseResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionRunBusy"/> record.</summary>
    /// <param name="activeRunId">The run currently holding the session's lease.</param>
    public SessionRunBusy(RunId activeRunId) => ActiveRunId = activeRunId;

    /// <summary>Gets the run currently holding the session's lease.</summary>
    public RunId ActiveRunId { get; init; }
}
