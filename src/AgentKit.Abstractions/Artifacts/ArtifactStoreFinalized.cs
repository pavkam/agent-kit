// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a store atomically published staged bytes and returns their first portable reference.</summary>
public sealed record ArtifactStoreFinalized: ArtifactStoreFinalizeResult
{
    /// <summary>Initializes a store publication success.</summary>
    /// <param name="reference">The committed immutable reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ArtifactStoreFinalized(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }

    /// <summary>Gets the committed immutable reference.</summary>
    public ArtifactReference Reference { get; }
}
