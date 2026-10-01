// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Plans scheduling, timeout, retry pacing, and result normalization for validated calls under one exact policy reference.</summary>
/// <remarks>
/// A policy is a pure planner: it performs no I/O, authorizes nothing, and cannot widen the captured descriptor's
/// declared effects or the scheduling hints it declared. Implementations must be safe to call concurrently.
/// </remarks>
public interface IToolExecutionPolicy
{
    /// <summary>Gets the exact policy key and revision this instance implements.</summary>
    /// <value>The immutable reference the selector matches ordinally and version-exactly.</value>
    public ToolExecutionPolicyReference Reference { get; }

    /// <summary>Plans a group of validated calls that all name <see cref="Reference"/>.</summary>
    /// <param name="calls">The initialized validated calls in source order.</param>
    /// <param name="context">The nonnull batch identity and planning instant.</param>
    /// <param name="cancellationToken">Cancels planning.</param>
    /// <returns>One prepared call per input call in the same order, or a typed rejection of the whole group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="calls"/> is uninitialized or a call names a different policy reference.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ToolExecutionPlanResult> PlanAsync(
        ImmutableArray<ValidatedToolCall> calls,
        ToolExecutionPolicyContext context,
        CancellationToken cancellationToken = default);
}
