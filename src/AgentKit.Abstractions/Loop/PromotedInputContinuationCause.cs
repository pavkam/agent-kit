// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Indicates that an already selected input promotion requires another request.</summary>
public sealed record PromotedInputContinuationCause: RunContinuationCause
{
    /// <summary>Initializes promoted-input evidence.</summary>
    /// <param name="snapshot">The immutable selection snapshot subject to session revalidation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="snapshot"/> contains no selected admissions.</exception>
    public PromotedInputContinuationCause(InputPromotionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfDefaultOrEmpty(snapshot.AdmissionIds);
        Snapshot = snapshot;
    }

    /// <summary>Gets the exact promotion evidence supplied by the I/O owner.</summary>
    public InputPromotionSnapshot Snapshot { get; }
}
