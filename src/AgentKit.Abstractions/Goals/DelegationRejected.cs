// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a delegation refused before any child goal was created.</summary>
public sealed record DelegationRejected: DelegationResult
{
    /// <summary>Initializes a rejection result.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <param name="rejection">The content-safe reason.</param>
    /// <param name="extensions">The forward-compatible extension data.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rejection"/> or <paramref name="extensions"/> is null.</exception>
    public DelegationRejected(DelegationId id, DelegationRejection rejection, ExtensionData extensions)
        : base(id, extensions)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        Rejection = rejection;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public DelegationRejection Rejection { get; }
}
