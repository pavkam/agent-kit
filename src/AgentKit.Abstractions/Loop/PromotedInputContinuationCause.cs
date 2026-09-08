// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies I/O-owned evidence that already selected input promotion requires another model request.</summary>
/// <remarks>The selection was made under the input coordinator's ordering rules. This cause does not admit or promote input again and becomes stale when affected promotion evidence changes.</remarks>
public sealed record PromotedInputContinuationCause: RunContinuationCause
{
    /// <summary>Initializes evidence for a continuation caused by selected input promotion.</summary>
    /// <param name="snapshot">The non-null immutable promotion snapshot containing at least one selected admission and subject to session-owner revalidation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> contains no selected admissions.</exception>
    public PromotedInputContinuationCause(InputPromotionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfDefaultOrEmpty(snapshot.AdmissionIds);
        Snapshot = snapshot;
    }

    /// <summary>Gets the exact promotion evidence supplied by the I/O owner.</summary>
    /// <value>A non-null immutable selection snapshot with at least one admission identity; it does not grant mutable queue access.</value>
    public InputPromotionSnapshot Snapshot { get; }
}
