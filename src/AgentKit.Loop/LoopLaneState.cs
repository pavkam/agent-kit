// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Tracks the one execution lane identity and live total-state revision a run's session operations observe.</summary>
/// <remarks>
/// <para>
/// Every session operation the loop performs for one run — the history load, every turn's context, every commit,
/// and the eventual continuation decision and lane release — must agree on exactly which lane they are operating
/// against and exactly which revision of that lane's installed state they last observed. When the run was durably
/// admitted through the session lane protocol (<see cref="AgentRunRequest.LaneAdmission"/> is not
/// <see langword="null"/>), both facts come from that admission: <see cref="ExecutionLaneId"/> is the lane
/// acceptance installed, and the initial <see cref="OperationStateRevision"/> is the revision acceptance recorded.
/// When the run carries no admission — a caller driving the loop directly, outside the engine's lane protocol —
/// this still derives a lane identity from the session, matching the convention the engine itself uses
/// (<c>new ExecutionLaneId(sessionId.Value)</c>), so every session operation for the run names one consistent
/// lane instead of leaving it null in some places and fabricated in others.
/// </para>
/// <para>
/// <see cref="OperationStateRevision"/> is mutable because an in-run operation that durably advances the lane's
/// installed state — for example promoting newly admitted input mid-run — changes the revision the loop must
/// present to every later operation on the same lane, including the eventual release. This type carries that one
/// live value by reference through the run so every reader after such an advance observes it, rather than each
/// call site continuing to reason from the value admission first installed.
/// </para>
/// </remarks>
internal sealed class LoopLaneState
{
    /// <summary>Initializes the lane state a run observes from the moment it starts.</summary>
    /// <param name="executionLaneId">The lane every session operation for this run must name.</param>
    /// <param name="operationStateRevision">The lane's total-state revision as of the start of the run.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="executionLaneId"/> or <paramref name="operationStateRevision"/> is default.
    /// </exception>
    public LoopLaneState(ExecutionLaneId executionLaneId, OperationStateRevision operationStateRevision)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default);
        ExecutionLaneId = executionLaneId;
        OperationStateRevision = operationStateRevision;
    }

    /// <summary>Gets the lane every session operation this run performs must name.</summary>
    /// <value>A non-default lane identity, constant for the lifetime of the run.</value>
    public ExecutionLaneId ExecutionLaneId { get; }

    /// <summary>Gets or sets the lane's total-state revision as last observed by this run.</summary>
    /// <value>A non-default revision, initially the value installed at admission (or 1 without one).</value>
    public OperationStateRevision OperationStateRevision { get; set; }
}
