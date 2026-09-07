// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a successfully normalized immutable candidate.</summary>
public sealed record IdentityNormalized: IdentityNormalizationResult
{
    /// <summary>Initializes a successful normalization result.</summary>
    /// <param name="identity">The current normalized candidate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    public IdentityNormalized(ExecutionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
    }

    /// <summary>Gets the normalized identity candidate.</summary>
    public ExecutionIdentity Identity { get; }
}
