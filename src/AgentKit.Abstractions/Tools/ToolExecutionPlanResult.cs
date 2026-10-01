// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed planned and rejected outcomes of one <see cref="IToolExecutionPolicy.PlanAsync"/> call.</summary>
/// <remarks>Neither outcome authorizes or invokes a call; a rejection is a typed decision, never a nullable success.</remarks>
public abstract record ToolExecutionPlanResult
{
    /// <summary>Initializes one of the two supported planning outcomes.</summary>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed planning family.</exception>
    private protected ToolExecutionPlanResult() =>
        ArgumentException.ThrowIfNotEqual(this is ToolExecutionPlanned or ToolExecutionPlanRejected, true, "result");

    /// <summary>Copies the base state of a supported immutable planning outcome.</summary>
    /// <param name="original">The nonnull original result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is null.</exception>
    /// <exception cref="ArgumentException">The constructed runtime type is outside the closed planning family.</exception>
    protected ToolExecutionPlanResult(ToolExecutionPlanResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(this is ToolExecutionPlanned or ToolExecutionPlanRejected, true, "result");
    }
}
