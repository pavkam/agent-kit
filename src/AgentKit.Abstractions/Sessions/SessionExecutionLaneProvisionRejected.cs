// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that protected access was rejected before lane state was read or mutated.</summary>
public sealed record SessionExecutionLaneProvisionRejected
    : SessionExecutionLaneProvisionResult
{
    /// <summary>Initializes a content-free protected-access rejection.</summary><param name="safeMessage">The nonblank diagnostic safe for callers.</param><exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionExecutionLaneProvisionRejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-free rejection description.</summary><value>A nonblank message containing no session payload.</value>
    public string SafeMessage { get; }
}
