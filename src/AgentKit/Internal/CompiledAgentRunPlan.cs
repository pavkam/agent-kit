// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

/// <summary>Reports that run-plan compilation produced a usable plan.</summary>
/// <remarks>The plan is immutable. Holding this result does not by itself keep the creating scope alive.</remarks>
internal sealed record CompiledAgentRunPlan: AgentRunPlanCompilationResult
{
    /// <summary>Captures a successfully compiled plan.</summary>
    /// <param name="plan">The non-null compiled plan.</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
    internal CompiledAgentRunPlan(AgentRunPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plan = plan;
    }

    /// <summary>Gets the compiled plan.</summary>
    /// <value>The activation the runtime drives. It does not include a fabricated run identity.</value>
    internal AgentRunPlan Plan { get; }
}
