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
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="promoted"/> is default, null-bearing, misordered, uncommitted, or inconsistent with snapshot address and lane.</exception>
    public InputPromoted(InputPromotionSnapshot snapshot, ImmutableArray<AdmittedInput> promoted, SessionVersion sessionVersion)
    {
        ArgumentException.ThrowIfInvalidPromotedInputs(snapshot, promoted);
        Snapshot = snapshot; Promoted = promoted; SessionVersion = sessionVersion;
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
}
