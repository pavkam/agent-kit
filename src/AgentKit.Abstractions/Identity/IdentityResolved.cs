// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports successful normalization or derivation of a trusted execution identity.</summary>
public sealed record IdentityResolved: IdentityResolutionResult
{
    /// <summary>Initializes a successful result.</summary>
    /// <param name="identity">The immutable resolved identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    public IdentityResolved(ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
    }

    /// <summary>Gets the resolved identity.</summary>
    public ExecutionIdentity Identity { get; }
}
