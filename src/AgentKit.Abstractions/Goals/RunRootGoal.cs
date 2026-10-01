// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Derives the stable identities of the implicit root goal and attempt that represent one run.</summary>
/// <remarks>
/// A run that delegates has no caller-created goal, so the delegation coordinator materializes one root goal and one
/// active attempt per run on first use. The identities are the run's own value, so every caller derives the same goal and
/// a retried delegation from the same run reaches the same parent without a lookup. The mapping is one-to-one and
/// reversible by construction; it carries no authority.
/// </remarks>
public static class RunRootGoal
{
    /// <summary>Derives the root goal identity for a run.</summary>
    /// <param name="runId">The run that owns the root goal.</param>
    /// <returns>The goal identity carrying the run's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default.</exception>
    public static GoalId GoalIdFor(RunId runId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        return new GoalId(runId.Value);
    }

    /// <summary>Derives the root attempt identity for a run.</summary>
    /// <param name="runId">The run that owns the root attempt.</param>
    /// <returns>The attempt identity carrying the run's value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="runId"/> is default.</exception>
    public static GoalAttemptId AttemptIdFor(RunId runId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        return new GoalAttemptId(runId.Value);
    }

    /// <summary>Determines whether a goal is the implicit root goal of a run.</summary>
    /// <param name="goalId">The goal to classify.</param>
    /// <param name="runId">The run.</param>
    /// <returns><see langword="true"/> when the goal carries the run's value.</returns>
    public static bool IsRootOf(GoalId goalId, RunId runId) => goalId.Value == runId.Value;
}
