// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the session branch and execution lane into which a call's durable records are appended.</summary>
/// <remarks>The target identifies a position, not authority: every append still passes the session coordinator's own authorization and lane checks.</remarks>
public sealed record ToolCallSessionTarget
{
    /// <summary>Initializes a target.</summary>
    /// <param name="branchId">The nondefault branch that receives the records.</param>
    /// <param name="executionLaneId">The nondefault lane that owns the run's mutations, or null for an explicitly session-wide operation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="branchId"/> or a present <paramref name="executionLaneId"/> is default.</exception>
    public ToolCallSessionTarget(BranchId branchId, ExecutionLaneId? executionLaneId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);
        if (executionLaneId is { } laneId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(laneId, default, nameof(executionLaneId));
        }

        BranchId = branchId;
        ExecutionLaneId = executionLaneId;
    }

    /// <summary>Gets the branch that receives the records.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the lane that owns the run's mutations.</summary>
    /// <value>Null only for explicitly session-wide operations.</value>
    public ExecutionLaneId? ExecutionLaneId { get; }
}
