// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an atomically committed input promotion or reconciliation of that exact prior promotion.</summary>
/// <remarks>The result identifies the durable selection and resulting records. It does not claim that a provider request has consumed the promoted content or that the run has settled.</remarks>
public sealed record InputPromoted: InputPromotionResult
{
    /// <summary>Initializes evidence for an atomically committed or reconciled promotion.</summary>
    /// <param name="snapshot">The non-null exact selection, ownership, cutoff, and target-turn evidence used by the promotion.</param>
    /// <param name="promoted">The non-default immutable records in the snapshot's committed selection order.</param>
    /// <param name="sessionVersion">The session version observed after the committed promotion transition.</param>
    /// <param name="committedCursor">The lane-owned branch tip after the atomic transition.</param>
    /// <param name="operationStateRevision">The total-state revision installed by this promotion.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> or <paramref name="committedCursor"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="promoted"/> is default, null-bearing, misordered, uncommitted, or inconsistent with snapshot address and lane.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationStateRevision"/> is default.</exception>
    public InputPromoted(
        InputPromotionSnapshot snapshot,
        ImmutableArray<AdmittedInput> promoted,
        SessionVersion sessionVersion,
        SessionBranchCursor committedCursor,
        OperationStateRevision operationStateRevision)
    {
        ArgumentException.ThrowIfInvalidPromotedInputs(snapshot, promoted);
        ArgumentNullException.ThrowIfNull(committedCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default);
        Snapshot = snapshot; Promoted = promoted; SessionVersion = sessionVersion;
        CommittedCursor = committedCursor; OperationStateRevision = operationStateRevision;
    }
    /// <summary>Gets the exact selection and ownership evidence for the promotion.</summary>
    /// <value>A non-null snapshot for the committed or reconciled plan, retained to prevent duplicate consumption.</value>
    public InputPromotionSnapshot Snapshot { get; }
    /// <summary>Gets the admitted records consumed by the promotion.</summary>
    /// <value>A non-default immutable collection in committed selection order, with each record marked by its promotion sequence.</value>
    public ImmutableArray<AdmittedInput> Promoted { get; }
    /// <summary>Gets the session version observed after the promotion committed.</summary>
    /// <value>The post-commit version for subsequent session coordination; it is not a branch identity or an input cutoff.</value>
    public SessionVersion SessionVersion { get; }
    /// <summary>Gets the lane-owned branch tip after the atomic transition.</summary>
    /// <value>The tip a caller must observe before a later admission, promotion, or release on this lane.</value>
    public SessionBranchCursor CommittedCursor { get; }
    /// <summary>Gets the total-state revision installed by this promotion.</summary>
    /// <value>The revision a later release or promotion on the same accepted run must present.</value>
    public OperationStateRevision OperationStateRevision { get; }
}
