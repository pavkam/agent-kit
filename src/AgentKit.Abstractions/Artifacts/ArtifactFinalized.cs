// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns the first portable reference to atomically published content.</summary>
public sealed record ArtifactFinalized: ArtifactFinalizeResult
{
    /// <summary>Initializes a publication success.</summary>
    /// <param name="reference">The committed immutable reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public ArtifactFinalized(ArtifactReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
    }
    /// <summary>Gets the committed immutable reference.</summary>
    public ArtifactReference Reference { get; }
}
