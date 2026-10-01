// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no single target could be chosen.</summary>
public sealed record DelegationTargetRejected: DelegationTargetSelectionResult
{
    /// <summary>Initializes a rejected selection.</summary>
    /// <param name="rejection">The content-safe reason, typically unknown or ambiguous target.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> is null.</exception>
    public DelegationTargetRejected(DelegationRejection rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public DelegationRejection Rejection { get; }
}
