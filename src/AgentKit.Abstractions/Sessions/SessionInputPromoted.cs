// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one atomically committed mid-run input promotion, or reconciliation of that exact prior commit.</summary>
/// <remarks>The result identifies the durable selection now marked promoted and the resulting branch/version evidence. It does not claim that a provider request has consumed the promoted content or that the run has settled.</remarks>
public sealed record SessionInputPromoted: SessionInputPromotionResult
{
    /// <summary>Initializes evidence for an atomically committed or reconciled mid-run promotion.</summary>
    /// <param name="promoted">The nonempty immutable records in committed selection order, each carrying a non-null promotion sequence.</param>
    /// <param name="committedCursor">The lane-owned branch tip after the atomic transition.</param>
    /// <param name="sessionVersion">The canonical whole-session version observed after the committed transition.</param>
    /// <param name="operationStateRevision">The total-state revision installed by this promotion.</param>
    /// <exception cref="ArgumentException"><paramref name="promoted"/> is default, empty, null-bearing, duplicated, or contains an unpromoted or misordered record.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="committedCursor"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationStateRevision"/> is default.</exception>
    public SessionInputPromoted(
        ImmutableArray<AdmittedInput> promoted,
        SessionBranchCursor committedCursor,
        SessionVersion sessionVersion,
        OperationStateRevision operationStateRevision)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(promoted);
        ArgumentException.ThrowIfContainsNull(promoted);
        ArgumentNullException.ThrowIfNull(committedCursor);
        ArgumentOutOfRangeException.ThrowIfEqual(operationStateRevision, default);
        var seen = new HashSet<AdmissionId>();
        for (var index = 0; index < promoted.Length; index++)
        {
            var input = promoted[index];
            if (input.PromotedSequence is null || !seen.Add(input.AdmissionId))
            {
                throw new ArgumentException("Promoted records must be uniquely committed and marked promoted.", nameof(promoted));
            }

            if (index > 0 && input.AdmittedSequence.Value <= promoted[index - 1].AdmittedSequence.Value)
            {
                throw new ArgumentException("Promoted records must be reported in ascending admitted-sequence order.", nameof(promoted));
            }
        }

        Promoted = promoted;
        CommittedCursor = committedCursor;
        SessionVersion = sessionVersion;
        OperationStateRevision = operationStateRevision;
    }

    /// <summary>Gets the admitted records consumed by the promotion.</summary>
    /// <value>A nonempty immutable collection in committed selection order; each record's promotion sequence is now set.</value>
    public ImmutableArray<AdmittedInput> Promoted { get; }

    /// <summary>Gets the lane-owned branch tip after the atomic transition.</summary>
    /// <value>The tip that must be observed before a later admission, promotion, or release on this lane.</value>
    public SessionBranchCursor CommittedCursor { get; }

    /// <summary>Gets the session version observed after the promotion committed.</summary>
    /// <value>The post-commit version for subsequent session coordination; it is not a branch identity or an input cutoff.</value>
    public SessionVersion SessionVersion { get; }

    /// <summary>Gets the total-state revision installed by this promotion.</summary>
    /// <value>The revision a later release or promotion on the same accepted run must expect.</value>
    public OperationStateRevision OperationStateRevision { get; }
}
