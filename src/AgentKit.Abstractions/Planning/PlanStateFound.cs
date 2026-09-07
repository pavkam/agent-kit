// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the current plan after a successful observation or mutation.</summary>
public sealed record PlanStateFound: PlanStateResult
{
    /// <summary>Initializes a successful plan-state result.</summary>
    /// <param name="plan">The current immutable plan revision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
    public PlanStateFound(WorkPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plan = plan;
    }

    /// <summary>Gets the current immutable plan revision.</summary>
    public WorkPlan Plan { get; init; }
}
