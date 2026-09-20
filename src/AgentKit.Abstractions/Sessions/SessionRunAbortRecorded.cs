// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a cancel marker was committed for one accepted run, or that an equivalent prior abort was replayed.</summary>
/// <remarks>
/// The first commit records the marker, prunes that run's pending admissions, and advances the lane and operation
/// revisions together. An equivalent retry returns this result with <see cref="Existing"/> set and does not advance
/// those revisions again or delete unrelated admissions.
/// </remarks>
public sealed record SessionRunAbortRecorded: SessionRunAbortResult
{
    /// <summary>Initializes an abort receipt.</summary>
    /// <param name="newVersion">The canonical whole-session version after the abort commit.</param>
    /// <param name="laneRevision">The lane revision after the abort commit.</param>
    /// <param name="stateRevision">The accepted operation's total-state revision after the abort commit.</param>
    /// <param name="existing">Whether this is an equivalent replay of a previously committed abort.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="laneRevision"/> or <paramref name="stateRevision"/> is default.
    /// </exception>
    public SessionRunAbortRecorded(
        SessionVersion newVersion,
        SessionLaneRevision laneRevision,
        OperationStateRevision stateRevision,
        bool existing)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(laneRevision, default);
        ArgumentOutOfRangeException.ThrowIfEqual(stateRevision, default);
        NewVersion = newVersion;
        LaneRevision = laneRevision;
        StateRevision = stateRevision;
        Existing = existing;
    }

    /// <summary>Gets the committed session version.</summary>
    /// <value>The version after the atomic abort, including an equivalent replay of that same commit.</value>
    public SessionVersion NewVersion { get; }

    /// <summary>Gets the committed lane revision.</summary>
    /// <value>The positive lane revision installed by the abort, unchanged on replay.</value>
    public SessionLaneRevision LaneRevision { get; }

    /// <summary>Gets the committed operation-state revision.</summary>
    /// <value>The positive total-state revision installed by the abort, unchanged on replay.</value>
    public OperationStateRevision StateRevision { get; }

    /// <summary>Gets whether this is a replay.</summary>
    /// <value><see langword="true"/> when an identical prior abort was reconciled without a second commit.</value>
    public bool Existing { get; }
}
