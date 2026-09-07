// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that optimistic plan revision evidence is stale.</summary>
public sealed record PlanStateConflict: PlanStateResult
{
    /// <summary>Initializes a conflict result.</summary>
    /// <param name="currentRevision">The current revision, or null when the plan does not exist.</param>
    public PlanStateConflict(PlanRevision? currentRevision) => CurrentRevision = currentRevision;

    /// <summary>Gets the current revision, or null when the plan does not exist.</summary>
    public PlanRevision? CurrentRevision { get; init; }
}
