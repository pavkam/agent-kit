// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the scheduling, timeout, retry, and result-normalization decisions an execution policy made for one call.</summary>
/// <remarks>The plan is immutable policy output. It grants no authority and never replaces the captured descriptor's declared effects.</remarks>
public sealed record ToolExecutionPlan
{
    /// <summary>Initializes a validated plan.</summary>
    /// <param name="scheduling">The effective scheduling hints, which a policy may tighten but the descriptor's own hints bound.</param>
    /// <param name="retry">The nonnull retry pacing for this call's attempts.</param>
    /// <param name="invocationTimeout">The positive deadline allowed for each invocation attempt.</param>
    /// <param name="normalization">The captured normalization rules that must name the selected execution policy.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="invocationTimeout"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="normalization"/> carries no execution-policy reference.</exception>
    public ToolExecutionPlan(
        ToolExecutionHints scheduling,
        ToolRetryPolicy retry,
        TimeSpan invocationTimeout,
        ToolResultNormalizationSnapshot normalization)
    {
        ArgumentNullException.ThrowIfNull(scheduling);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(invocationTimeout, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(normalization);
        ArgumentNullException.ThrowIfNull(normalization.ExecutionPolicy, nameof(normalization));
        Scheduling = scheduling;
        Retry = retry;
        InvocationTimeout = invocationTimeout;
        Normalization = normalization;
    }

    /// <summary>Gets the effective scheduling hints.</summary>
    public ToolExecutionHints Scheduling { get; }

    /// <summary>Gets the retry pacing.</summary>
    public ToolRetryPolicy Retry { get; }

    /// <summary>Gets the per-attempt invocation deadline.</summary>
    /// <value>A positive duration measured from the instant the attempt starts.</value>
    public TimeSpan InvocationTimeout { get; }

    /// <summary>Gets the captured normalization rules.</summary>
    /// <value>A snapshot whose <see cref="ToolResultNormalizationSnapshot.ExecutionPolicy"/> is the selected policy.</value>
    public ToolResultNormalizationSnapshot Normalization { get; }
}
