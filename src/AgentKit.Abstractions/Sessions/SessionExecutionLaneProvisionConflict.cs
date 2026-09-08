// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports deterministic lane-provision evidence that did not match current canonical state.</summary>
public sealed record SessionExecutionLaneProvisionConflict
    : SessionExecutionLaneProvisionResult
{
    /// <summary>Initializes a content-free conflict.</summary><param name="safeMessage">The nonblank diagnostic safe for callers.</param><exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionExecutionLaneProvisionConflict(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-free conflict description.</summary><value>A nonblank message containing no session payload.</value>
    public string SafeMessage { get; }
}
