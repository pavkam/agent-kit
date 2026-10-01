// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that reconciliation established a terminal disposition for the intent.</summary>
public sealed record ArtifactReconciled: ArtifactReconciliationResult
{
    /// <summary>Initializes a terminal reconciliation outcome.</summary>
    /// <param name="disposition">The established disposition.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="disposition"/> is undefined.</exception>
    public ArtifactReconciled(ArtifactReconciliationDisposition disposition)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(disposition);
        Disposition = disposition;
    }

    /// <summary>Gets the established disposition.</summary>
    public ArtifactReconciliationDisposition Disposition { get; }
}
