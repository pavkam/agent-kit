// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Pairs one validated call with the plan its selected execution policy produced.</summary>
/// <remarks>A prepared call is not accepted: authorization and the accepted record must still succeed before an invoker starts.</remarks>
public sealed record PreparedToolCall
{
    /// <summary>Initializes a prepared call.</summary>
    /// <param name="call">The nonnull validated call.</param>
    /// <param name="executionPlan">The nonnull plan whose normalization names the same execution policy as <paramref name="call"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The plan's execution policy differs from the call's selected policy reference.</exception>
    public PreparedToolCall(ValidatedToolCall call, ToolExecutionPlan executionPlan)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(executionPlan);
        ArgumentException.ThrowIfNotEqual(executionPlan.Normalization.ExecutionPolicy, call.ExecutionPolicy, nameof(executionPlan));
        Call = call;
        ExecutionPlan = executionPlan;
    }

    /// <summary>Gets the validated call.</summary>
    public ValidatedToolCall Call { get; }

    /// <summary>Gets the policy-produced plan.</summary>
    public ToolExecutionPlan ExecutionPlan { get; }
}
