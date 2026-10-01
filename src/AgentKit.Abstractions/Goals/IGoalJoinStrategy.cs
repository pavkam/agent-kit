// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Decides a parent's join deterministically from durable child results.</summary>
/// <remarks>A strategy is a pure function of its evaluation request: it does no I/O, reads no clock, and never observes task completion order. Implementations are thread-safe and may be singletons.</remarks>
public interface IGoalJoinStrategy
{
    /// <summary>Gets the key this strategy answers for.</summary>
    /// <value>A non-blank key that is unique among registered strategies.</value>
    public GoalJoinStrategyKey Key { get; }

    /// <summary>Evaluates one snapshot of a parent's children.</summary>
    /// <param name="request">The evaluation request.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>A pending, satisfied, or unsatisfiable decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<GoalJoinDecision> EvaluateAsync(GoalJoinEvaluationRequest request, CancellationToken cancellationToken = default);
}
