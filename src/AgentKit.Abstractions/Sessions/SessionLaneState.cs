// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one provisioned execution lane's current durable revision, branch cursor, and optional accepted run.</summary>
/// <remarks>
/// This is the truthful discovery evidence a caller needs before admitting input into, promoting input into, or
/// starting a run on, an already-provisioned lane without retaining its own non-durable copy of that state. When
/// <see cref="AcceptedState"/> is present the lane is currently busy with that run; when it is
/// <see langword="null"/> the lane is idle and eligible for a new <see cref="SessionRunStartRequest"/>. While a run
/// is accepted, admitting further input independently advances <see cref="Revision"/> and
/// <see cref="BranchCursor"/> past <see cref="SessionAcceptedRunState.LaneRevision"/> and
/// <see cref="SessionAcceptedRunState.CommittedCursor"/>, which remain the fixed values installed at acceptance; a
/// mid-run <see cref="SessionInputPromotionRequest"/> observes this state's live revision and cursor, not the
/// accepted state's acceptance-time snapshot.
/// </remarks>
public sealed record SessionLaneState
{
    /// <summary>Initializes consistent lane-state evidence.</summary>
    /// <param name="executionLaneId">The non-default lane identity this state describes.</param>
    /// <param name="revision">The lane's current positive revision.</param>
    /// <param name="branchCursor">The lane's current branch cursor.</param>
    /// <param name="acceptedState">The lane's currently installed accepted run, or <see langword="null"/> when idle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="branchCursor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="executionLaneId"/> or <paramref name="revision"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="acceptedState"/> is present and its lane does not match.</exception>
    public SessionLaneState(
        ExecutionLaneId executionLaneId,
        SessionLaneRevision revision,
        SessionBranchCursor branchCursor,
        SessionAcceptedRunState? acceptedState)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(executionLaneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(revision, default);
        ArgumentNullException.ThrowIfNull(branchCursor);
        if (acceptedState is not null)
        {
            ArgumentException.ThrowIfNotEqual(acceptedState.ExecutionLaneId, executionLaneId, nameof(acceptedState));
        }

        ExecutionLaneId = executionLaneId;
        Revision = revision;
        BranchCursor = branchCursor;
        AcceptedState = acceptedState;
    }

    /// <summary>Gets the lane identity this state describes.</summary>
    /// <value>The non-default identity supplied at provisioning.</value>
    public ExecutionLaneId ExecutionLaneId { get; }

    /// <summary>Gets the lane's current revision.</summary>
    /// <value>
    /// The positive revision advanced by provisioning, every accepted run installed on this lane, every
    /// subsequent admission, and every mid-run promotion; it may exceed
    /// <see cref="SessionAcceptedRunState.LaneRevision"/> while a run is accepted.
    /// </value>
    public SessionLaneRevision Revision { get; }

    /// <summary>Gets the lane's current branch cursor.</summary>
    /// <value>
    /// The tip a caller must observe to admit input, promote input, or accept a run without a stale-cursor
    /// rejection; it may differ from <see cref="SessionAcceptedRunState.CommittedCursor"/> while a run is accepted.
    /// </value>
    public SessionBranchCursor BranchCursor { get; }

    /// <summary>Gets the lane's currently installed accepted run, if any.</summary>
    /// <value>The complete accepted state while the lane is busy; <see langword="null"/> while it is idle.</value>
    public SessionAcceptedRunState? AcceptedState { get; }
}
